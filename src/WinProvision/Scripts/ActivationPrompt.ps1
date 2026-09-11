param([ValidateSet('en-US','pt-BR')][string]$UiLanguage='en-US',[switch]$NoShow)
$ErrorActionPreference='Stop'
$state=Join-Path $env:LOCALAPPDATA 'WinProvision'
$languagePath=Join-Path $state 'Apps\language.json'
if(Test-Path -LiteralPath $languagePath){
 try {
  $saved=(Get-Content -LiteralPath $languagePath -Raw -Encoding UTF8 | ConvertFrom-Json).Language
  if($saved -in @('pt-BR','en-US')){$UiLanguage=$saved}
 } catch {}
}
. (Join-Path $PSScriptRoot 'Localization.ps1')
Add-Type -AssemblyName PresentationFramework
[xml]$xaml=@'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Width="460" SizeToContent="Height" ResizeMode="NoResize"
        WindowStartupLocation="CenterScreen" Background="#F3F6F5"
        FontFamily="Segoe UI" FontSize="14">
 <StackPanel Margin="24">
  <TextBlock Name="Heading" FontSize="22" FontWeight="SemiBold" Foreground="#12343B" TextWrapping="Wrap"/>
  <TextBlock Name="Question" Margin="0,16,0,0" TextWrapping="Wrap"/>
  <TextBlock Name="Internet" Margin="0,10,0,0" TextWrapping="Wrap" FontWeight="SemiBold"/>
  <TextBlock Name="Details" Margin="0,10,0,0" TextWrapping="Wrap" Foreground="#53686E"/>
  <TextBlock Name="ErrorText" Margin="0,12,0,0" TextWrapping="Wrap" Foreground="#B42318" Visibility="Collapsed"/>
  <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,22,0,0">
   <Button Name="Later" MinWidth="100" Padding="14,8" Margin="0,0,10,0" IsCancel="True"/>
   <Button Name="Confirm" MinWidth="110" Padding="14,8" Background="#087F73" Foreground="White"/>
  </StackPanel>
 </StackPanel>
</Window>
'@
$script:activationWindow=[Windows.Markup.XamlReader]::Load([Xml.XmlNodeReader]::new($xaml))
$activationWindow.Title=T 'WinProvision - Windows activation'
foreach($entry in @(@('Heading','Windows activation'),@('Question','Would you like to start the activator?'),@('Internet','An internet connection is required.'),@('Details','Confirm to open the interactive terminal. Windows may ask for administrator permission. You can also open this window later from the desktop shortcut.'))){
 $activationWindow.FindName($entry[0]).Text=T $entry[1]
}
$script:confirmButton=$activationWindow.FindName('Confirm')
$script:laterButton=$activationWindow.FindName('Later')
$confirmButton.Content=T 'Confirm'
$laterButton.Content=T 'Not now'
$laterButton.Add_Click({$activationWindow.Close()})
$confirmButton.Add_Click({
 $confirmButton.IsEnabled=$false
 try {
  $activation=Join-Path $PSScriptRoot 'activateWindows.cmd'
  if(-not(Test-Path -LiteralPath $activation)){throw 'Activation script is missing.'}
  Start-Process -FilePath ($env:WINDIR+'\System32\cmd.exe') -ArgumentList ('/d /k ""'+$activation+'""') -WindowStyle Normal -ErrorAction Stop
  $activationWindow.Close()
 } catch {
  $errorText=$activationWindow.FindName('ErrorText')
  $errorText.Text=T 'Could not open the activator. Try again or check the installation files.'
  $errorText.Visibility='Visible'
  try {
   New-Item -ItemType Directory -Path $state -Force | Out-Null
   $_.Exception.Message | Out-File (Join-Path $state 'Activation-launch.log') -Append -Encoding UTF8
  } catch {}
  $confirmButton.IsEnabled=$true
 }
})
if(-not $NoShow){$null=$activationWindow.ShowDialog()}
