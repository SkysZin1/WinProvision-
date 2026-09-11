param([string]$AnswerPath=(Join-Path $PSScriptRoot '..\src\WinProvision\bin\Release\net8.0-windows\test-generated-11.xml'))
$ErrorActionPreference='Stop'
$fixture=Join-Path $PSScriptRoot ('..\artifacts\loader-tests\'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Panther') -Force | Out-Null
[xml]$xml=Get-Content -LiteralPath $AnswerPath -Raw -Encoding UTF8
$ns=[Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('wp','https://winprovision.local/schema/1')
$bootstrap=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($xml.SelectSingleNode('//wp:Bootstrap',$ns).InnerText))
$start=$bootstrap.IndexOf('foreach($entry')
$end=$bootstrap.IndexOf('$launchTarget=')
if($start -lt 0 -or $end -le $start){throw 'Payload extraction block not found.'}
$extraction=$bootstrap.Substring($start,$end-$start)
$expected='Portugu'+[char]0xEA+'s; instala'+[char]0xE7+[char]0xE3+'o; '+[char]0x2014
$xml.SelectSingleNode('//wp:Localization',$ns).InnerText=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($expected))
# Simulate an ASCII-only Setup rewrite, then execute only file extraction, never the payloads.
$x=[xml]([Text.Encoding]::ASCII.GetString([Text.Encoding]::ASCII.GetBytes($xml.OuterXml)))
$d=Join-Path $fixture 'extracted'
New-Item -ItemType Directory -Path $d -Force | Out-Null
& ([scriptblock]::Create($extraction))
if([IO.File]::ReadAllText((Join-Path $d 'Translations.json')) -ne $expected){throw 'Unicode payload was corrupted during extraction.'}
$cmdBytes=[IO.File]::ReadAllBytes((Join-Path $d 'activateWindows.cmd'))
if($cmdBytes.Length -ge 3 -and $cmdBytes[0] -eq 239 -and $cmdBytes[1] -eq 187 -and $cmdBytes[2] -eq 191){throw 'CMD payload must not have a UTF-8 BOM.'}
$cmdText=[Text.Encoding]::UTF8.GetString($cmdBytes)
if($cmdText -match '(?<!\r)\n' -or -not $cmdText.EndsWith("`r`n`r`n")){throw 'CMD payload requires CRLF and a final empty line.'}
Write-Output 'PASS: Unicode payload survives an ASCII XML rewrite and UTF-8 extraction.'
foreach($name in @('Bootstrap','Cleanup')){
 $node=$xml.SelectSingleNode('//wp:'+ $name,$ns)
 if(-not $node){throw "Missing $name"}
 # Replace executable payloads with harmless markers before running the actual loader.
 $node.InnerText=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("# multiline payload`n[IO.File]::WriteAllText((Join-Path `$env:WINDIR '$name.txt'),'$name - '+[char]0xE7)"))
}
[IO.File]::WriteAllText((Join-Path $fixture 'Panther\unattend.xml'),[regex]::Replace($xml.OuterXml,'\s+',' '))
foreach($name in @('Bootstrap','Cleanup')){
 $command=@($xml.SelectNodes('//*[local-name()="Path" or local-name()="CommandLine"]') | Where-Object {$_.InnerText.Contains('Extensions.'+$name)})
 if($command.Count -ne 1){throw "Expected one loader for $name"}
 if($command[0].InnerText.Length -gt 259){throw 'Loader exceeds Setup limit.'}
 $info=[Diagnostics.ProcessStartInfo]::new()
 $info.FileName="$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
 $info.Arguments=$command[0].InnerText.Substring('powershell.exe '.Length)
 $info.UseShellExecute=$false;$info.CreateNoWindow=$true
 $info.EnvironmentVariables['WINDIR']=[IO.Path]::GetFullPath($fixture)
 $process=[Diagnostics.Process]::Start($info)
 $null=$process.Handle
 if(-not $process.WaitForExit(15000)){$process.Kill();throw 'Loader timed out.'}
 if($process.ExitCode -ne 0){throw 'Loader process failed.'}
 $process.Dispose()
 if([IO.File]::ReadAllText((Join-Path $fixture ($name+'.txt'))) -ne ($name+' - '+[char]0xE7)){throw 'Embedded payload did not execute correctly.'}
 Write-Output ("PASS: $name loader executes embedded XML payload in Windows PowerShell 5.1 ($($command[0].InnerText.Length) characters).")
}
$xml.SelectSingleNode('//wp:Cleanup',$ns).InnerText=''
$xml.Save((Join-Path $fixture 'Panther\unattend.xml'))
$info.RedirectStandardError=$true
$process=[Diagnostics.Process]::Start($info)
$null=$process.Handle
if(-not $process.WaitForExit(15000)){$process.Kill();throw 'Missing-payload check timed out.'}
if($process.ExitCode -eq 0){throw 'Missing payload silently succeeded.'}
$process.Dispose()
Write-Output 'PASS: missing payload returns failure instead of silently skipping setup.'
