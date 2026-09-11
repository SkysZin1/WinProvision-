param([string]$AnswerPath=(Join-Path $PSScriptRoot '..\src\WinProvision\bin\Release\net8.0-windows\test-generated-11.xml'))
$ErrorActionPreference='Stop'
[xml]$xml=Get-Content -LiteralPath $AnswerPath -Raw -Encoding UTF8
$launcher=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($xml.SelectSingleNode('//*[local-name()="Launcher"]').InnerText))
$fixture=Join-Path $PSScriptRoot ('..\artifacts\launcher-tests\'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $fixture 'LaunchApps.ps1'),$launcher)
Set-Content (Join-Path $fixture 'Provisioner.ps1') '$global:pickerOpened++'
Set-Content (Join-Path $fixture 'activateWindows.cmd') 'This fixture must never execute.'
Set-Content (Join-Path $fixture 'ActivateWindows.js') 'This fixture must never execute.'
$originalData=$env:LOCALAPPDATA
$global:activationLaunches=0
$global:pickerOpened=0
$global:failLaunch=$false
function Start-Process {
 param($FilePath,$ArgumentList,$WindowStyle,$ErrorAction,[switch]$Wait)
 if($Wait -or $WindowStyle -ne 'Hidden' -or $FilePath -notlike '*wscript.exe' -or $ArgumentList -notlike '*ActivateWindows.js*'){throw 'Startup must open only the hidden-host confirmation prompt.'}
 $global:activationLaunches++
 if($global:failLaunch){throw 'Simulated launch failure'}
}
try {
 $env:LOCALAPPDATA=$fixture
 & (Join-Path $fixture 'LaunchApps.ps1')
 & (Join-Path $fixture 'LaunchApps.ps1')
 if($global:activationLaunches -ne 1 -or $global:pickerOpened -ne 2){throw 'Launch-once behavior failed.'}
 $env:LOCALAPPDATA=Join-Path $fixture 'failure'
 $global:failLaunch=$true
 & (Join-Path $fixture 'LaunchApps.ps1')
 if($global:pickerOpened -ne 3){throw 'Launch failure blocked App Picker.'}
 if(Test-Path (Join-Path $env:LOCALAPPDATA 'WinProvision\activation-prompt-started.flag')){throw 'Failed launch marked as started.'}
 Write-Output 'PASS: parallel launch, launch-once behavior, and picker recovery. No activation script was executed.'
} finally {$env:LOCALAPPDATA=$originalData}
