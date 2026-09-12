param([ValidateSet('pt-BR','en-US')][string]$UiLanguage='en-US')
$ErrorActionPreference='Stop'
$root=Join-Path (Split-Path $PSScriptRoot) 'src\WinProvision'
. (Join-Path $root 'Scripts\Localization.ps1')
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
function Invoke-WebRequest {
 param($Uri,$OutFile,[switch]$UseBasicParsing,$ErrorAction,$TimeoutSec,$Method)
 if($Uri -eq 'https://cdn.winget.microsoft.com/cache/source.msix'){
  if($global:wpAppCase -in @('offline','captive-portal')){throw 'Download service unreachable'}
  if($Method -ne 'Head'){throw 'Connectivity check must not download the package'}
  return [pscustomobject]@{StatusCode=200}
 }
 if($Uri -eq 'https://www.msftconnecttest.com/connecttest.txt'){
  if($global:wpAppCase -in @('offline','connectivity-endpoint-failure')){throw 'No connection'}
  return [pscustomobject]@{StatusCode=200;Content=if($global:wpAppCase -eq 'captive-portal'){'Sign in'}else{'Microsoft Connect Test'}}
 }
 if(-not $OutFile -or $Uri -notlike 'https://*'){throw 'Unexpected download'}
 [IO.File]::WriteAllText($OutFile,'Mock installer: never execute')
}
function Get-AuthenticodeSignature {param($LiteralPath) [pscustomobject]@{Status=if($global:wpAppCase -eq 'nvidia-untrusted'){'NotSigned'}else{'Valid'};SignerCertificate=[pscustomobject]@{Subject='CN=NVIDIA Corporation, O=NVIDIA Corporation'}}}
function Start-Process {
 param($FilePath,$ArgumentList,$WindowStyle,[switch]$PassThru,[switch]$Wait,$RedirectStandardOutput,$RedirectStandardError,$Verb)
 if($FilePath -like '*NVIDIA-app.exe') {if($global:wpAppCase -ne 'nvidia' -or $Verb -ne 'RunAs'){throw 'Unexpected NVIDIA launch.'};$global:wpAppCalls++;return [pscustomobject]@{ExitCode=0}}
 if($FilePath -notlike '*WindowsApps\winget.exe'){throw 'Unexpected executable.'}
 if($ArgumentList -eq '--version'){
  $probe=[pscustomobject]@{ExitCode=if($global:wpAppCase -eq 'broken-winget'){1}else{0}}
  $probe | Add-Member ScriptMethod WaitForExit {param($timeout) return ($global:wpAppCase -ne 'winget-timeout')}
  $probe | Add-Member ScriptMethod Kill {}
  $probe | Add-Member ScriptMethod Dispose {}
  return $probe
 }
 if($ArgumentList[0] -ne 'install' -or $ArgumentList[3] -ne '--exact' -or $ArgumentList -notcontains '--silent' -or $ArgumentList -notcontains '--disable-interactivity'){throw 'Unsafe install arguments.'}
 if($ArgumentList[2] -notin @('7zip.7zip','Mozilla.Firefox','9PLM9XGG6VKS')){throw 'Unexpected package.'}
 if($ArgumentList[2] -eq '9PLM9XGG6VKS' -and $ArgumentList[5] -ne 'msstore'){throw 'Wrong ChatGPT source.'}
 $global:wpAppCalls++
 if($global:wpAppCase -eq 'cancel'){[IO.File]::WriteAllText((Join-Path $global:wpAppJob 'stop'),'stop')}
 [pscustomobject]@{ExitCode=if($global:wpAppCase -eq 'failure' -and $global:wpAppCalls -eq 1){123}elseif($global:wpAppCase -eq 'already-installed'){[int]0x8A150061}else{0}}
}
foreach($scenario in @('success','failure','cancel','invalid','missing-winget','already-installed','store','nvidia','nvidia-untrusted','offline','captive-portal','broken-winget','winget-timeout','connectivity-endpoint-failure')) {
 $global:wpAppCase=$scenario;$global:wpAppCalls=0
 $global:wpAppJob=Join-Path $root ('artifacts\app-worker-tests\'+[guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory -Path $global:wpAppJob -Force | Out-Null
 $ids=if($scenario -eq 'invalid'){@('not-allowed; command')}elseif($scenario -eq 'store'){@('9PLM9XGG6VKS')}elseif($scenario -like 'nvidia*'){@('NVIDIA.App.Official')}else{@('7zip.7zip','Mozilla.Firefox')}
 @{Ids=$ids} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $global:wpAppJob 'request.json') -Encoding UTF8
 & (Join-Path $root 'Scripts\Provisioner.ps1') -Worker -JobPath $global:wpAppJob -UiLanguage $UiLanguage
 $result=Get-Content -LiteralPath (Join-Path $global:wpAppJob 'status.json') -Raw | ConvertFrom-Json
 if(-not $result.Done){throw 'Worker did not finish.'}
 if($scenario -eq 'connectivity-endpoint-failure' -and ($global:wpAppCalls -ne 2 -or @($result.Items | Where-Object Status -ne 'Completed').Count)){throw 'Fallback connectivity check failed.'}
 if($scenario -in @('offline','captive-portal','missing-winget','broken-winget','winget-timeout')){
  if($global:wpAppCalls -ne 0 -or $result.Items.Count -ne 2 -or @($result.Items | Where-Object Status -ne 'Failed').Count){throw 'Preflight did not prevent installation or preserve retry IDs.'}
  if($scenario -in @('offline','captive-portal') -and $result.Message -ne (T 'Internet access could not be verified. Connect to Wi-Fi or Ethernet, complete any network sign-in, then retry. No apps were installed.')){throw 'Internet guidance failed.'}
 }
 switch($scenario){
  'success' {if($global:wpAppCalls -ne 2 -or @($result.Items | Where-Object Status -ne 'Completed').Count){throw 'Success handling failed.'}}
  'failure' {if($global:wpAppCalls -ne 2 -or $result.Items[0].Status -ne 'Failed' -or $result.Items[1].Status -ne 'Completed'){throw 'Failure continuation failed.'}}
  'cancel' {if($global:wpAppCalls -ne 1 -or $result.Items[1].Status -ne 'Deferred'){throw 'Queue cancellation failed.'}}
  'invalid' {if($global:wpAppCalls -ne 0 -or $result.Message -ne (T 'Invalid app selection.')){throw 'Invalid selection was not rejected.'}}
  'missing-winget' {if($global:wpAppCalls -ne 0 -or $result.Message -notlike '*Microsoft Store*'){throw 'Missing WinGet guidance failed.'}}
  'already-installed' {if($global:wpAppCalls -ne 2 -or @($result.Items | Where-Object Status -ne 'Completed').Count){throw 'Already installed handling failed.'}}
  'store' {if($global:wpAppCalls -ne 1 -or $result.Items[0].Status -ne 'Completed'){throw 'Store handling failed.'}}
  'nvidia' {if($global:wpAppCalls -ne 1 -or $result.Items[0].Status -ne 'Completed'){throw 'NVIDIA handling failed.'}}
  'nvidia-untrusted' {if($global:wpAppCalls -ne 0 -or $result.Items[0].Status -ne 'Failed'){throw 'Untrusted NVIDIA installer was not blocked.'}}
 }
 Write-Output "PASS: $scenario"
 if($scenario -eq 'failure'){
  $retryIds=@($result.Items | Where-Object Status -eq 'Failed' | Select-Object -ExpandProperty Id)
  $global:wpAppCase='success';$global:wpAppCalls=0
  @{Ids=$retryIds} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $global:wpAppJob 'request.json') -Encoding UTF8
  & (Join-Path $root 'Scripts\Provisioner.ps1') -Worker -JobPath $global:wpAppJob -UiLanguage $UiLanguage
  $retryResult=Get-Content -LiteralPath (Join-Path $global:wpAppJob 'status.json') -Raw | ConvertFrom-Json
  if($global:wpAppCalls -ne 1 -or $retryResult.Items.Count -ne 1 -or $retryResult.Items[0].Id -ne '7zip.7zip' -or $retryResult.Items[0].Status -ne 'Completed'){throw 'Retry reinstalled a successful app or failed to recover.'}
  Write-Output 'PASS: retry failure without repeating successful app'
 }
}
Write-Output 'PASS: 15 worker scenarios. No software was downloaded, registered or installed.'
