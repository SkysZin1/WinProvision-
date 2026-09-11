$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
[xml]$project=Get-Content (Join-Path $root 'src\WinProvision\WinProvision.csproj')
$version=[string]$project.Project.PropertyGroup.Version
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid release version.'}
$output=Join-Path $PSScriptRoot ('releases\WinProvision-desktop-v'+$version)
$zip=$output+'.zip'
if((Test-Path $output) -or (Test-Path $zip)){throw 'Release output already exists. Review it before rebuilding.'}
& dotnet publish (Join-Path $root 'src\WinProvision\WinProvision.csproj') -c Release --self-contained false -p:DebugType=None -p:DebugSymbols=false -o $output
if($LASTEXITCODE -ne 0){throw 'Publish failed.'}
foreach($name in @('README.md','CHANGELOG.md','LICENSE')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $output}
Copy-Item -LiteralPath (Join-Path $root ('docs\'+$version+'-Release.md')) -Destination (Join-Path $output 'Release-Notes.md')
Copy-Item -LiteralPath (Join-Path $root 'src\WinProvision\Assets\AppIcons\README.md') -Destination (Join-Path $output 'Icon-Credits.md')
Copy-Item -LiteralPath (Join-Path $root 'src\WinProvision\Assets\AppIcons\LICENSE.md') -Destination (Join-Path $output 'Simple-Icons-LICENSE.md')
$files=@(Get-ChildItem -LiteralPath $output -Recurse -File)
if($files | Where-Object { $_.Extension -in @('.pdb','.cs','.xaml','.iso','.vhdx') -or $_.Name -like '*test*' -or $_.Name -eq 'autounattend.xml' }){throw 'Unexpected development or private file in package.'}
foreach($name in @('WinProvision.exe','WinProvision.dll','Scripts\Build-Usb.ps1','Scripts\Provisioner.ps1','Scripts\ActivationPrompt.ps1','Scripts\activateWindows.cmd','Assets\Translations.json')){
 if(-not(Test-Path (Join-Path $output $name))){throw "Missing release file: $name"}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($output,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {
 if($archive.Entries.Count -ne $files.Count){throw 'Archive file count mismatch.'}
 foreach($entry in $archive.Entries){
  $stream=$entry.Open();$hasher=[Security.Cryptography.SHA256]::Create()
  try{$hash=[BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$hasher.Dispose()}
  if($hash -ne (Get-FileHash -LiteralPath (Join-Path $output $entry.FullName)).Hash){throw 'Archive content mismatch.'}
 }
} finally {$archive.Dispose()}
$checksum=(Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zip+'.sha256'),$checksum+'  '+[IO.Path]::GetFileName($zip)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output ("Verified release: "+$zip+" ("+$files.Count+" files)")
