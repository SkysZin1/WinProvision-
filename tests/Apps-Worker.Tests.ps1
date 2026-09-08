$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$global:wpAppCase=''
$global:wpAppCalls=0
# Mock every operation which can register or install a package.
function Test-Path {
 param($LiteralPath)
 if($LiteralPath -like '*WindowsApps\winget.exe'){return ($global:wpAppCase -ne 'missing-winget')}
 Microsoft.PowerShell.Management\Test-Path -LiteralPath $LiteralPath
}
function Add-AppxPackage { throw 'Registration must not be called in these tests.' }
function Start-Sleep { param($Seconds) }
function Invoke-WebRequest {param($Uri,$OutFile,[switch]$UseBasicParsing,$ErrorAction) [IO.File]::WriteAllText($OutFile,'Mock installer: never execute')}
function Get-AuthenticodeSignature {param($LiteralPath) [pscustomobject]@{Status=if($global:wpAppCase -eq 'nvidia-untrusted'){'NotSigned'}else{'Valid'};SignerCertificate=[pscustomobject]@{Subject='CN=NVIDIA Corporation, O=NVIDIA Corporation'}}}
function Start-Process {
 param($FilePath,$ArgumentList,$WindowStyle,[switch]$PassThru,[switch]$Wait,$RedirectStandardOutput,$RedirectStandardError,$Verb)
 if($FilePath -like '*NVIDIA-app.exe') {if($global:wpAppCase -ne 'nvidia' -or $Verb -ne 'RunAs'){throw 'Unexpected NVIDIA launch.'};$global:wpAppCalls++;return [pscustomobject]@{ExitCode=0}}
 if($FilePath -notlike '*WindowsApps\winget.exe'){throw 'Unexpected executable.'}
 if($ArgumentList[0] -ne 'install' -or $ArgumentList[3] -ne '--exact' -or $ArgumentList -notcontains '--silent' -or $ArgumentList -notcontains '--disable-interactivity'){throw 'Unsafe install arguments.'}
 if($ArgumentList[2] -notin @('7zip.7zip','Mozilla.Firefox','9NT1R1C2HH7J')){throw 'Unexpected package.'}
 if($ArgumentList[2] -eq '9NT1R1C2HH7J' -and $ArgumentList[5] -ne 'msstore'){throw 'Wrong ChatGPT source.'}
 $global:wpAppCalls++
 if($global:wpAppCase -eq 'cancel'){[IO.File]::WriteAllText((Join-Path $global:wpAppJob 'stop'),'stop')}
 [pscustomobject]@{ExitCode=if($global:wpAppCase -eq 'failure' -and $global:wpAppCalls -eq 1){123}elseif($global:wpAppCase -eq 'already-installed'){[int]0x8A150061}else{0}}
}
foreach($scenario in @('success','failure','cancel','invalid','missing-winget','already-installed','store','nvidia','nvidia-untrusted')) {
 $global:wpAppCase=$scenario;$global:wpAppCalls=0
 $global:wpAppJob=Join-Path $root ('artifacts\app-worker-tests\'+[guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory -Path $global:wpAppJob -Force | Out-Null
 $ids=if($scenario -eq 'invalid'){@('not-allowed; command')}elseif($scenario -eq 'store'){@('9NT1R1C2HH7J')}elseif($scenario -like 'nvidia*'){@('NVIDIA.App.Official')}else{@('7zip.7zip','Mozilla.Firefox')}
 @{Ids=$ids} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $global:wpAppJob 'request.json') -Encoding UTF8
 & (Join-Path $root 'Scripts\Provisioner.ps1') -Worker -JobPath $global:wpAppJob
 $result=Get-Content -LiteralPath (Join-Path $global:wpAppJob 'status.json') -Raw | ConvertFrom-Json
 if(-not $result.Done){throw 'Worker did not finish.'}
 switch($scenario){
  'success' {if($global:wpAppCalls -ne 2 -or @($result.Items | Where-Object Status -ne 'Completed').Count){throw 'Success handling failed.'}}
  'failure' {if($global:wpAppCalls -ne 2 -or $result.Items[0].Status -ne 'Failed' -or $result.Items[1].Status -ne 'Completed'){throw 'Failure continuation failed.'}}
  'cancel' {if($global:wpAppCalls -ne 1 -or $result.Items[1].Status -ne 'Deferred'){throw 'Queue cancellation failed.'}}
  'invalid' {if($global:wpAppCalls -ne 0 -or $result.Message -notlike '*Invalid app selection*'){throw 'Invalid selection was not rejected.'}}
  'missing-winget' {if($global:wpAppCalls -ne 0 -or $result.Message -notlike '*Microsoft Store*'){throw 'Missing WinGet guidance failed.'}}
  'already-installed' {if($global:wpAppCalls -ne 2 -or @($result.Items | Where-Object Status -ne 'Completed').Count){throw 'Already installed handling failed.'}}
  'store' {if($global:wpAppCalls -ne 1 -or $result.Items[0].Status -ne 'Completed'){throw 'Store handling failed.'}}
  'nvidia' {if($global:wpAppCalls -ne 1 -or $result.Items[0].Status -ne 'Completed'){throw 'NVIDIA handling failed.'}}
  'nvidia-untrusted' {if($global:wpAppCalls -ne 0 -or $result.Items[0].Status -ne 'Failed'){throw 'Untrusted NVIDIA installer was not blocked.'}}
 }
 Write-Output "PASS: $scenario"
}
Write-Output 'PASS: 9 worker scenarios. No software was downloaded, registered or installed.'
