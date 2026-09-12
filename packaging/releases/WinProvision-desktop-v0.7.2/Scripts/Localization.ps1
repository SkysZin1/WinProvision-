# Shared by the desktop picker, its worker and the USB writer. Keep persisted keys in English.
$translationPath=Join-Path $PSScriptRoot 'Translations.json'
if(-not (Test-Path -LiteralPath $translationPath)){$translationPath=Join-Path $PSScriptRoot '..\Assets\Translations.json'}
$script:translationBundle=Get-Content -LiteralPath $translationPath -Raw -Encoding UTF8 | ConvertFrom-Json
$script:translationMap=@{}
foreach($entry in $script:translationBundle.Translations.PSObject.Properties){$script:translationMap[$entry.Name]=$entry.Value}
function T([string]$text) {
 if($script:UiLanguage -eq 'pt-BR' -and $script:translationMap.ContainsKey($text)){return $script:translationMap[$text]}
 return $text
}
function TF([string]$text,[object[]]$values) {
 return [string]::Format([Globalization.CultureInfo]::GetCultureInfo($script:UiLanguage),(T $text),$values)
}

