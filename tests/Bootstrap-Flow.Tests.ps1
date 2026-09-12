param([string]$AnswerPath=(Join-Path $PSScriptRoot '..\src\WinProvision\bin\Release\net8.0-windows\test-generated-11.xml'))
$ErrorActionPreference='Stop'
[xml]$xml=Get-Content -LiteralPath $AnswerPath -Raw -Encoding UTF8
$bootstrap=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($xml.SelectSingleNode('//*[local-name()="Bootstrap"]').InnerText))
$fixture=Join-Path $PSScriptRoot ('..\artifacts\bootstrap-tests\'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Panther'),(Join-Path $fixture 'Temp') -Force | Out-Null
# Replace every extracted executable with inert content before exercising orchestration.
foreach($name in @('Script','ClassicMenu','ActivationPrompt','Launcher','Provisioner','LocalizationScript','ActivateWindows')) {
 $node=$xml.SelectSingleNode('//*[local-name()="'+$name+'"]')
 if($node){$node.InnerText=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('# inert fixture'))}
}
$xml.SelectSingleNode('//*[local-name()="Script"]').InnerText=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('$global:configured=$true'))
$xml.Save((Join-Path $fixture 'Panther\unattend.xml'))
$oldWindows=$env:WINDIR
$global:configured=$false
$global:registered=$false
$global:unloaded=$false
# Mock system operations; no host registry, default profile, or desktop is modified.
function reg.exe {
 param($operation,$key,$path)
 if($operation -eq 'unload'){$global:unloaded=$true}
 $global:LASTEXITCODE=0
}
function New-Object { throw 'Simulated unavailable desktop shortcut' }
function New-Item {
 param($Path,$ItemType,[switch]$Force)
 if($Path -like 'Registry::*'){return}
 Microsoft.PowerShell.Management\New-Item -Path $Path -ItemType $ItemType -Force:$Force
}
function New-ItemProperty {
 param($Path,$Name,$PropertyType,$Value,[switch]$Force)
 if($Path -notlike 'Registry::*' -or $Value -notlike '*wscript.exe*LaunchApps.js*'){throw 'Unexpected registration'}
 $global:registered=$true
}
try {
 $env:WINDIR=[IO.Path]::GetFullPath($fixture)
 & ([scriptblock]::Create($bootstrap))
 if(-not $global:registered -or -not $global:configured -or -not $global:unloaded){throw 'Shortcut failure interrupted setup.'}
 Write-Output 'PASS: shortcut failure preserves extraction, first-login registration, configuration and hive cleanup.'
 # Exercise the real JScript host with a harmless replacement, never the picker or activator.
 $launcherDir=Join-Path $fixture 'Setup\Scripts\WinProvision'
 Set-Content -LiteralPath (Join-Path $launcherDir 'LaunchApps.ps1') -Value '[IO.File]::WriteAllText((Join-Path $PSScriptRoot "hidden-launch.ok"),"ok")'
 $hostInfo=[Diagnostics.ProcessStartInfo]::new()
 $hostInfo.FileName=Join-Path $oldWindows 'System32\cscript.exe'
 $hostInfo.Arguments='//B //NoLogo "'+[IO.Path]::GetFullPath((Join-Path $launcherDir 'LaunchApps.js'))+'"'
 $hostInfo.UseShellExecute=$false;$hostInfo.CreateNoWindow=$true
 $hostProcess=[Diagnostics.Process]::Start($hostInfo)
 if(-not $hostProcess.WaitForExit(10000) -or $hostProcess.ExitCode -ne 0){throw 'JScript launcher failed.'}
 $deadline=[DateTime]::UtcNow.AddSeconds(10)
 while(-not(Test-Path (Join-Path $launcherDir 'hidden-launch.ok')) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 if(-not(Test-Path (Join-Path $launcherDir 'hidden-launch.ok'))){throw 'Hidden launcher did not execute its inert payload.'}
 Write-Output 'PASS: Windows Script Host starts the harmless PowerShell payload.'
 # Corrupted required resources must produce a diagnostic, not a successful setup.
 $xml.SelectSingleNode('//*[local-name()="Provisioner"]').InnerText='invalid base64!'
 $xml.Save((Join-Path $fixture 'Panther\unattend.xml'))
 $failed=$false
 try { & ([scriptblock]::Create($bootstrap)) } catch {$failed=$true}
 if(-not $failed -or -not (Test-Path (Join-Path $fixture 'Temp\WinProvision-Bootstrap.log'))){throw 'Missing bootstrap failure diagnostic.'}
 Write-Output 'PASS: invalid payload fails with a bootstrap diagnostic. No real configuration or activation was executed.'
} finally {$env:WINDIR=$oldWindows}
