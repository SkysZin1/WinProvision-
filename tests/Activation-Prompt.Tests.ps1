param([string]$AnswerPath=(Join-Path $PSScriptRoot '..\src\WinProvision\bin\Release\net8.0-windows\test-generated-11.xml'))
$ErrorActionPreference='Stop'
[xml]$xml=Get-Content -LiteralPath $AnswerPath -Raw -Encoding UTF8
$fixture=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\artifacts\activation-prompt-tests\'+[guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
foreach($pair in @(@('ActivationPrompt','ActivationPrompt.ps1'),@('Localization','Translations.json'),@('LocalizationScript','Localization.ps1'))){
 $text=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($xml.SelectSingleNode('//*[local-name()="'+$pair[0]+'"]').InnerText))
 [IO.File]::WriteAllText((Join-Path $fixture $pair[1]),$text,[Text.UTF8Encoding]::new($true))
}
Set-Content (Join-Path $fixture 'activateWindows.cmd') 'INERT FIXTURE - MUST NEVER RUN'
$savedData=$env:LOCALAPPDATA
$global:launchCount=0
$global:simulateFailure=$false
function Start-Process {
 param($FilePath,$ArgumentList,$WindowStyle,$ErrorAction)
 if($FilePath -notlike '*cmd.exe' -or $ArgumentList -notlike '/d /k ""*activateWindows.cmd""' -or $WindowStyle -ne 'Normal'){throw 'Wrong interactive activation invocation.'}
 $global:launchCount++
 if($global:simulateFailure){throw 'Simulated launch failure'}
}
function Click($button){$button.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))}
try {
 $env:LOCALAPPDATA=$fixture
 foreach($lang in @('pt-BR','en-US')){
  . (Join-Path $fixture 'ActivationPrompt.ps1') -UiLanguage $lang -NoShow
  if($global:launchCount -ne 0){throw 'Prompt executed activation without confirmation.'}
  if($activationWindow.FindName('Internet').Text -notmatch 'internet'){throw 'Missing internet notice.'}
  if($lang -eq 'pt-BR' -and $confirmButton.Content -ne 'Confirmar'){throw 'Portuguese translation missing.'}
  $visual=$activationWindow.Content
  $visual.Background=$activationWindow.Background
  $visual.Measure([Windows.Size]::new(460,800));$visual.Arrange([Windows.Rect]::new(0,0,460,$visual.DesiredSize.Height));$visual.UpdateLayout()
  $image=[Windows.Media.Imaging.RenderTargetBitmap]::new(460,[int][Math]::Ceiling($visual.DesiredSize.Height),96,96,[Windows.Media.PixelFormats]::Pbgra32)
  $image.Render($visual)
  $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($image))
  $file=[IO.File]::Create((Join-Path $fixture ($lang+'.png')));try{$encoder.Save($file)}finally{$file.Dispose()}
  Click $laterButton
 }
 if($global:launchCount -ne 0){throw 'Cancel started activation.'}
 . (Join-Path $fixture 'ActivationPrompt.ps1') -UiLanguage en-US -NoShow
 $activationWindow.Close()
 if($global:launchCount -ne 0){throw 'Closing the window started activation.'}
 . (Join-Path $fixture 'ActivationPrompt.ps1') -UiLanguage en-US -NoShow
 Click $confirmButton
 if($global:launchCount -ne 1){throw 'Confirmation did not start exactly one terminal.'}
 . (Join-Path $fixture 'ActivationPrompt.ps1') -UiLanguage en-US -NoShow
 $global:simulateFailure=$true
 Click $confirmButton
 if(-not $confirmButton.IsEnabled -or $activationWindow.FindName('ErrorText').Visibility -ne 'Visible'){throw 'Launch failure did not allow retry.'}
 $activationWindow.Close()
 Write-Output 'PASS: no automatic execution; cancel/close, confirmation, launch failure, bilingual UI. Activation was mocked.'
 Write-Output ('UI snapshots: '+$fixture)
} finally {$env:LOCALAPPDATA=$savedData}
