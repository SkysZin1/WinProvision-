$ErrorActionPreference='Stop'
# Load only the preparation function; never execute the writer or real disk cmdlets.
$writer=Join-Path (Split-Path $PSScriptRoot) 'src\WinProvision\Scripts\Build-Usb.ps1'
$UiLanguage='en-US'
. (Join-Path (Split-Path $writer) 'Localization.ps1')
$tokens=$null; $errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($writer,[ref]$tokens,[ref]$errors)
if($errors.Count) { throw 'Writer parse failed.' }
$function=$ast.Find({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Reset-UsbLayout'},$true)
if(-not $function) { throw 'Preparation function missing.' }
Invoke-Expression $function.Extent.Text
function Assert-Target {
 $script:reads++
 if($script:changed -and $script:reads -gt 1) { throw 'USB identity changed' }
 [pscustomobject]@{Number=99;PartitionStyle=$script:style;NumberOfPartitions=$script:partitions}
}
function Clear-Disk {
 param($Number,[switch]$RemoveData,[switch]$RemoveOEM,$Confirm)
 $script:clears++
 $script:style=$script:afterClear
 if(-not $script:retain) { $script:partitions=0 }
}
function Update-Disk { param($Number) }
function Initialize-Disk {
 param($Number,$PartitionStyle)
 $script:initializes++
 if($script:style -ne 'RAW') { throw 'The disk has already been initialized.' }
 if($script:initFail) { throw 'Simulated initialization failure' }
 $script:style=$PartitionStyle
}
function Set-Disk { param($Number,$PartitionStyle) $script:converts++; $script:style=$PartitionStyle }
foreach($case in @('raw','clear-to-raw','clear-stays-gpt','clear-stays-mbr','remaining-partitions','identity-changed','initialize-fails','unknown-style')) {
 $script:style=if($case -eq 'raw'){'RAW'}else{'GPT'}
 $script:afterClear=switch($case){'clear-stays-gpt'{'GPT'} 'clear-stays-mbr'{'MBR'} 'unknown-style'{'Unknown'} default{'RAW'}}
 $script:partitions=if($case -eq 'raw'){0}else{2}
 $script:retain=$case -eq 'remaining-partitions'
 $script:changed=$case -eq 'identity-changed'
 $script:initFail=$case -eq 'initialize-fails'
 $script:reads=0; $script:clears=0; $script:initializes=0; $script:converts=0
 $failure=$null
 try { $result=Reset-UsbLayout @{} } catch { $failure=$_.Exception.Message }
 $expected=switch($case){'remaining-partitions'{'partitions remain'} 'identity-changed'{'identity changed'} 'initialize-fails'{'Simulated initialization failure'} 'unknown-style'{'Unexpected USB partition style'}}
 if($expected) {
  if($failure -notlike "*$expected*") { throw "${case}: unexpected result: $failure" }
  if($case -ne 'initialize-fails' -and ($script:initializes -ne 0 -or $script:converts -ne 0)) { throw "${case}: mutation after rejected state" }
 } else {
  if($failure -or $result.PartitionStyle -ne 'GPT') { throw "${case}: $failure" }
  $expectedInit=if($case -in @('raw','clear-to-raw')){1}else{0}
  $expectedConvert=if($case -eq 'clear-stays-mbr'){1}else{0}
  $expectedClear=if($case -eq 'raw'){0}else{1}
  if($script:initializes -ne $expectedInit -or $script:converts -ne $expectedConvert -or $script:clears -ne $expectedClear) { throw "${case}: incorrect disk operations" }
 }
 Write-Output "PASS: $case"
}
Write-Output 'PASS: 8 layout scenarios. All disk operations were mocked; no physical disk was touched.'
