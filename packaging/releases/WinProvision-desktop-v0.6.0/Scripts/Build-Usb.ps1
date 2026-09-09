param([Parameter(Mandatory=$true)][string]$JobPath)
$ErrorActionPreference='Stop'
$UiLanguage=if([Globalization.CultureInfo]::CurrentUICulture.Name -like 'pt*'){'pt-BR'}else{'en-US'}
. (Join-Path $PSScriptRoot 'Localization.ps1')
$dir=Split-Path -LiteralPath $JobPath
$mountedByUs=$false
$iso=$null
$transcribing=$false
function Status([string]$text) { [IO.File]::WriteAllText((Join-Path $dir 'status.txt'),$text) }
function Assert-Target($job) {
 $d=Get-Disk -Number $job.DiskNumber
 if($d.BusType -ne 'USB' -or $d.IsBoot -or $d.IsSystem -or $d.IsReadOnly -or $d.IsOffline) { throw (T 'The selected disk is no longer a writable non-system USB disk.') }
 if([string]::IsNullOrWhiteSpace($job.UniqueId) -or $d.UniqueId -cne $job.UniqueId -or [uint64]$d.Size -ne [uint64]$job.Size) { throw (T 'USB identity changed. Refresh the device list and review again.') }
 foreach($path in @($job.IsoPath,$job.AnswerFile,$job.AppDirectory,$JobPath)) {
  $root=[IO.Path]::GetPathRoot($path)
  if($root -match '^[A-Za-z]:\\$') {
   $partition=Get-Partition -DriveLetter $root.Substring(0,1) -ErrorAction Stop
   if($partition.DiskNumber -eq $d.Number) { throw (T 'The USB contains the ISO, application or working files. Move them to another disk first.') }
  } else { throw (T 'Use local drive paths for the ISO and application.') }
 }
 return $d
}
function Reset-UsbLayout($job) {
 $disk=Assert-Target $job
 if($disk.PartitionStyle -ne 'RAW') { Clear-Disk -Number $disk.Number -RemoveData -RemoveOEM -Confirm:$false }
 # Refresh the provider after clearing: some USB devices still report an initialized layout.
 Update-Disk -Number $disk.Number
 $disk=Assert-Target $job
 if($disk.NumberOfPartitions -ne 0) { throw (T 'USB partitions remain after clearing. No new partition was created.') }
 switch([string]$disk.PartitionStyle) {
  'RAW' { Initialize-Disk -Number $disk.Number -PartitionStyle GPT | Out-Null }
  'GPT' { } # Already initialized and empty; do not initialize twice.
  'MBR' { Set-Disk -Number $disk.Number -PartitionStyle GPT }
  default { throw (TF 'Unexpected USB partition style: {0}' @($disk.PartitionStyle)) }
 }
 Update-Disk -Number $disk.Number
 $disk=Assert-Target $job
 if($disk.PartitionStyle -ne 'GPT') { throw (T 'USB preparation did not produce a GPT disk.') }
 return $disk
}
try {
 Start-Transcript -LiteralPath (Join-Path $dir 'build.log') -Force | Out-Null
 $transcribing=$true
 $job=Get-Content -LiteralPath $JobPath -Raw | ConvertFrom-Json
 if($job.InterfaceLanguage -in @('pt-BR','en-US')){$UiLanguage=$job.InterfaceLanguage}
 if($job.Windows -notin @('10','11')) { throw (T 'Invalid Windows target.') }
 $target=Assert-Target $job
 if($target.Size -lt 8GB) { throw (T 'An 8 GiB or larger USB disk is required.') }
 if([IO.Path]::GetExtension($job.IsoPath) -ine '.iso' -or -not (Test-Path -LiteralPath $job.IsoPath -PathType Leaf)) { throw (T 'Select an existing Windows ISO file.') }
 [xml]$answer=Get-Content -LiteralPath $job.AnswerFile -Raw
 if($answer.DocumentElement.LocalName -ne 'unattend') { throw (T 'Invalid answer file.') }
 Status (T '1 / 6  Checking the ISO and USB identity…')
 $iso=Get-DiskImage -ImagePath $job.IsoPath
 if(-not $iso.Attached) { $iso=Mount-DiskImage -ImagePath $job.IsoPath -PassThru; $mountedByUs=$true }
 $volumes=@($iso | Get-Volume | Where-Object DriveLetter)
 if($volumes.Count -ne 1) { throw (T 'The ISO must expose exactly one installation volume.') }
 $source=$volumes[0].DriveLetter+':\'
 foreach($rel in @('setup.exe','sources\boot.wim','efi\boot\bootx64.efi')) {
  if(-not (Test-Path -LiteralPath (Join-Path $source $rel))) { throw (TF 'This is not supported x64 UEFI Windows media: missing {0}' @($rel)) }
 }
 # Accept clean installation media only. Never silently carry third-party setup scripts.
 foreach($rel in @('autounattend.xml','unattend.xml','$OEM$','sources\$OEM$')) {
  if(Test-Path -LiteralPath (Join-Path $source $rel)) { throw (TF 'The ISO contains custom setup content ({0}). Use an unmodified official Microsoft ISO.' @($rel)) }
 }
 $imagePath=Join-Path $source 'sources\install.wim'
 if(-not (Test-Path -LiteralPath $imagePath)) { $imagePath=Join-Path $source 'sources\install.esd' }
 if(-not (Test-Path -LiteralPath $imagePath)) { throw (T 'The ISO needs sources\install.wim or install.esd. Pre-split ISO images are not supported in this release.') }
 $imageList=@(Get-WindowsImage -ImagePath $imagePath)
 if($imageList.Count -eq 0) { throw (T 'The installation image has no editions.') }
 $details=Get-WindowsImage -ImagePath $imagePath -Index $imageList[0].ImageIndex
 if([string]$details.Architecture -notin @('9','x64','amd64')) { throw (T 'Only x64 Windows images are supported in this release.') }
 $version=[version]$details.Version
 $actualWindows=if($version.Build -ge 22000){'11'}else{'10'}
 if($version.Major -ne 10 -or $actualWindows -ne $job.Windows) { throw (TF 'The ISO is Windows {0} ({1}), but the profile targets Windows {2}.' @($actualWindows,$version,$job.Windows)) }
 if($job.Edition -ne 'Ask during setup' -and -not ($imageList | Where-Object ImageName -eq "Windows $($job.Windows) $($job.Edition)")) { throw (T 'The requested edition does not exist in this ISO.') }
 if($job.Language -ne 'ISO default' -and $job.Language -notin @($details.Languages)) { throw (T 'The selected display language is not present in this ISO. Choose ISO default or use a matching ISO.') }
 $allFiles=@(Get-ChildItem -LiteralPath $source -Recurse -File -Force)
 $large=@($allFiles | Where-Object Length -ge 4GB)
 foreach($file in $large) { if($file.FullName -ine $imagePath -or $file.Extension -ine '.wim') { throw (TF 'FAT32 cannot store {0}. This release can split large WIM files only; use a Microsoft ISO with install.wim.' @($file.Name)) } }
 $total=($allFiles | Measure-Object Length -Sum).Sum
 $partitionSize=[uint64][Math]::Min(30GB,([Math]::Floor(($target.Size-16MB)/1MB)*1MB))
 if(($total+512MB) -gt $partitionSize) { throw (T 'The ISO does not fit in the USB boot partition (maximum 30 GiB, with working headroom).') }
 # Re-read identity and source-disk exclusions immediately before destructive work.
 $target=Assert-Target $job
 Status (T '2 / 6  Erasing the confirmed USB and creating its boot partition…')
 $target=Reset-UsbLayout $job
 $partition=New-Partition -DiskNumber $target.Number -Size $partitionSize -AssignDriveLetter
 $volume=$partition | Format-Volume -FileSystem FAT32 -NewFileSystemLabel 'WINPROVISION' -Confirm:$false -Force
 if(-not $volume.DriveLetter) { throw (T 'Windows did not assign a letter to the USB partition.') }
 $destination=$volume.DriveLetter+':\'
 Status (T '3 / 6  Copying Windows installation files…')
 $argsCopy=@($source,$destination,'/E','/R:1','/W:1','/COPY:DAT','/DCOPY:T','/XJ','/NFL','/NDL','/NP')
 if($large.Count -gt 0) { $argsCopy+=@('/XF','install.wim') }
 & robocopy.exe @argsCopy
 if($LASTEXITCODE -ge 8) { throw (TF 'Windows file copy failed (robocopy exit {0}).' @($LASTEXITCODE)) }
 if($large.Count -gt 0) {
  Status (T '4 / 6  Splitting install.wim for FAT32. This can take several minutes…')
  & dism.exe /English /Split-Image "/ImageFile:$imagePath" "/SWMFile:$(Join-Path $destination 'sources\install.swm')" /FileSize:3800 /CheckIntegrity
  if($LASTEXITCODE -ne 0) { throw (TF 'Image split failed (DISM exit {0}).' @($LASTEXITCODE)) }
 } else { Status (T '4 / 6  Installation image fits FAT32; no split required.') }
 Status (T '5 / 6  Writing your Windows configuration…')
 Copy-Item -LiteralPath $job.AnswerFile -Destination (Join-Path $destination 'autounattend.xml') -Force
 Status (T '6 / 6  Verifying copied files and generated configuration…')
 foreach($file in $allFiles) {
  if($large.Count -gt 0 -and $file.FullName -ieq $imagePath) { continue }
  $relative=$file.FullName.Substring($source.Length)
  $copy=Get-Item -LiteralPath (Join-Path $destination $relative)
  if($copy.Length -ne $file.Length) { throw (TF 'File verification failed: {0}' @($relative)) }
 }
 if($large.Count -gt 0) {
  $segments=@(Get-ChildItem -LiteralPath (Join-Path $destination 'sources') -Filter 'install*.swm')
  if($segments.Count -lt 2 -or ($segments | Where-Object { $_.Length -eq 0 -or $_.Length -ge 4GB })) { throw (T 'Split-image verification failed.') }
  Get-WindowsImage -ImagePath (Join-Path $destination 'sources\install.swm') | Out-Null
 }
 if((Get-FileHash -LiteralPath $job.AnswerFile).Hash -ne (Get-FileHash -LiteralPath (Join-Path $destination 'autounattend.xml')).Hash) { throw (T 'Answer-file checksum mismatch.') }
 Status (T 'USB ready. Safely eject it before unplugging.')
 @{Success=$true;Message=(T 'USB created and verified.')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dir 'result.json') -Encoding UTF8
} catch {
 $message=$_.Exception.Message
 Status ((T 'Failed: ')+$message)
 Write-Output $message
 @{Success=$false;Message=$message} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dir 'result.json') -Encoding UTF8
} finally {
 if($mountedByUs -and $iso) { Dismount-DiskImage -ImagePath $iso.ImagePath -ErrorAction SilentlyContinue | Out-Null }
 if($transcribing) { Stop-Transcript | Out-Null }
}
