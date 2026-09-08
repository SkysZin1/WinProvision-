# Development helper. Rebuild the offline vector catalog from the checked-in SVGs.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$catalog=@(
 @('Google.Chrome','googlechrome','#4285F4'),
 @('Mozilla.Firefox','firefoxbrowser','#FF7139'),
 @('Brave.Brave','brave','#FB542B'),
 @('7zip.7zip','7zip','#252A31'),
 @('VideoLAN.VLC','vlcmediaplayer','#E87500'),
 @('Spotify.Spotify','spotify','#16883D'),
 @('Discord.Discord','discord','#5865F2'),
 @('Valve.Steam','steam','#18364F'),
 @('TheDocumentFoundation.LibreOffice','libreoffice','#168A2F'),
 @('Microsoft.VisualStudioCode','visualstudiocode','#007ACC'),
 @('Notepad++.Notepad++','notepadplusplus','#44792C'),
 @('Git.Git','git','#F05032')
)
$definitions=[ordered]@{}
$extra=Get-Content -LiteralPath (Join-Path $root 'Assets\AdditionalApps.json') -Raw | ConvertFrom-Json
foreach($app in $extra){$catalog+=,@($app.Id,$app.IconKey,$app.Color)}
foreach($entry in $catalog){
 [xml]$svg=Get-Content -LiteralPath (Join-Path $root ('Assets\AppIcons\'+$entry[1]+'.svg')) -Raw
 $path=$svg.SelectSingleNode('//*[local-name()="path"]')
 if(-not $path -or $svg.DocumentElement.GetAttribute('viewBox') -ne '0 0 24 24'){throw "Unsupported icon: $($entry[1])"}
 $definitions[$entry[0]]=@{Color=$entry[2];Path=$path.GetAttribute('d')}
}
$json=$definitions | ConvertTo-Json -Depth 4
$block="# BEGIN BUNDLED APP ICONS`r`n"+'$iconDefinitions=ConvertFrom-Json @'+"'`r`n"+$json+"`r`n'@`r`n# END BUNDLED APP ICONS"
$scriptPath=Join-Path $PSScriptRoot 'Provisioner.ps1'
$text=[IO.File]::ReadAllText($scriptPath)
$extraBlock="# BEGIN ADDITIONAL APPS`r`n"+'$catalog+= (ConvertFrom-Json @'+"'`r`n"+($extra | ConvertTo-Json -Depth 5)+"`r`n'@)`r`n# END ADDITIONAL APPS"
$extraMatch=[regex]::new('(?s)# BEGIN ADDITIONAL APPS.*?# END ADDITIONAL APPS')
if($extraMatch.Matches($text).Count -ne 1){throw 'Additional app marker missing.'}
$text=$extraMatch.Replace($text,[Text.RegularExpressions.MatchEvaluator]{param($m) $extraBlock})
$match=[regex]::new('(?s)# BEGIN BUNDLED APP ICONS.*?# END BUNDLED APP ICONS')
if($match.Matches($text).Count -ne 1){throw 'Icon insertion marker missing or duplicated.'}
$text=$match.Replace($text,[Text.RegularExpressions.MatchEvaluator]{param($m) $block})
[IO.File]::WriteAllText($scriptPath,$text,[Text.UTF8Encoding]::new($true))
Write-Output "Bundled $($catalog.Count) offline vector icons."
