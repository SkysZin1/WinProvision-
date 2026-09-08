$ErrorActionPreference='Stop'
$workspace=Split-Path $PSScriptRoot
$testRoot=Join-Path $workspace 'artifacts\writer-guard-tests'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$global:wpTestDestructiveCalls=0
$global:wpTestScenario=''
function Clear-Disk { $global:wpTestDestructiveCalls++; throw 'Destructive mock must never be called' }
function Initialize-Disk { $global:wpTestDestructiveCalls++; throw 'Destructive mock must never be called' }
function New-Partition { $global:wpTestDestructiveCalls++; throw 'Destructive mock must never be called' }
function Format-Volume { $global:wpTestDestructiveCalls++; throw 'Destructive mock must never be called' }
function Get-Disk {
 [pscustomobject]@{Number=99;BusType=if($global:wpTestScenario -eq 'internal'){'NVMe'}else{'USB'};IsBoot=($global:wpTestScenario -eq 'system');IsSystem=($global:wpTestScenario -eq 'system');IsReadOnly=$false;IsOffline=$false;UniqueId=if($global:wpTestScenario -eq 'changed'){'CHANGED'}else{'TEST-USB'};Size=[uint64]16GB}
}
function Get-Partition { [pscustomobject]@{DiskNumber=if($global:wpTestScenario -eq 'source-on-usb'){99}else{0}} }
function Get-DiskImage { throw 'Mounting must not be reached by guard rejection tests' }
foreach($case in @('system','internal','changed','source-on-usb','invalid-iso')) {
 $global:wpTestScenario=$case
 $caseDir=Join-Path $testRoot $case
 New-Item -ItemType Directory -Path $caseDir -Force | Out-Null
 $jobFile=Join-Path $caseDir 'job.json'
 @{IsoPath=(Join-Path $workspace 'does-not-exist.iso');DiskNumber=99;UniqueId='TEST-USB';Size=[uint64]16GB;Windows='11';Edition='Pro';Language='ISO default';AnswerFile=(Join-Path $caseDir 'answer.xml');AppDirectory=$workspace} | ConvertTo-Json | Set-Content -LiteralPath $jobFile -Encoding UTF8
 & (Join-Path $workspace 'Scripts\Build-Usb.ps1') -JobPath $jobFile
 $result=Get-Content -LiteralPath (Join-Path $caseDir 'result.json') -Raw | ConvertFrom-Json
 if($result.Success -or $global:wpTestDestructiveCalls -ne 0) { throw "Guard failed: $case" }
 $expected=switch($case) { 'system' {'non-system USB'} 'internal' {'non-system USB'} 'changed' {'identity changed'} 'source-on-usb' {'contains the ISO'} 'invalid-iso' {'existing Windows ISO'} }
 if($result.Message -notlike "*$expected*") { throw "Unexpected failure in ${case}: $($result.Message)" }
 Write-Output "PASS: $case rejected before any destructive operation."
}
Write-Output 'PASS: 5 writer guard scenarios. Disk cmdlets were mocked; no physical disk was touched.'
