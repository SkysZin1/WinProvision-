using System.Text;
using System.IO;
using System.Xml.Linq;

namespace WinProvision;
public static class AnswerFile
{
 static readonly XNamespace Ns = "urn:schemas-microsoft-com:unattend";
 static readonly XNamespace Wcm = "http://schemas.microsoft.com/WMIConfig/2002/State";
 static readonly XNamespace Ext = "https://winprovision.local/schema/1";
 static XElement Payload(string name, string text) => new(Ext + name, new XAttribute("encoding", "base64"), Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));
 static string EmbeddedCommand(string name) => "powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \"$ErrorActionPreference='Stop';[xml]$x=gc $env:WINDIR\\Panther\\unattend.xml -Raw;iex([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($x.unattend.Extensions." + name + ")))\"";
 static XElement E(string name, object? value = null) => new(Ns + name, value);
 static XElement Component(string name, params object[] children) => new(Ns + "component",
  new XAttribute("name", name), new XAttribute("processorArchitecture", "amd64"), new XAttribute("publicKeyToken", "31bf3856ad364e35"),
  new XAttribute("language", "neutral"), new XAttribute("versionScope", "nonSxS"), children);
 static XElement Command(int order, string command) => E("RunSynchronousCommand", new object[] { new XAttribute(Wcm + "action", "add"), E("Order", order), E("Path", command) });
 static XElement Password(string password) => E("Password", new object[] { E("Value", password), E("PlainText", "true") });
 public static string Generate(BuildProfile p, string password, bool preview = false)
 {
  p.Validate();
  if (p.DesktopReady && (string.IsNullOrWhiteSpace(p.Username) || !p.Administrator || !p.AutoLogon || p.Language == "ISO default" || p.Keyboard == "Default" || p.TimeZone == "Default" || !new[] { "hide-online", "hide-wifi", "privacy-oobe" }.All(p.Options.Contains)))
   throw new ArgumentException(L.T("Automatic desktop setup requires a local administrator account, one-time logon, explicit language, keyboard and time zone, and the three OOBE options. Complete the Recommended setup details before continuing."));
  if (p.Username.Length > 0 && password.Length == 0 && !preview) throw new ArgumentException(L.T("Set a password for the local account, or leave the account name empty to create it during Windows Setup."));
  var root = E("unattend"); root.Add(new XAttribute(XNamespace.Xmlns + "wcm", Wcm));
  var pe = E("settings", new XAttribute("pass", "windowsPE"));
  var setup = Component("Microsoft-Windows-Setup");
  if (p.DesktopReady) setup.Add(E("UserData", E("AcceptEula", "true")));
  // Disk selection remains interactive: never generate DiskConfiguration / InstallTo.
  if (p.Edition != "Ask during setup")
   setup.Add(E("ImageInstall", E("OSImage", E("InstallFrom", E("MetaData", new object[] { new XAttribute(Wcm + "action", "add"), E("Key", "/IMAGE/NAME"), E("Value", $"Windows {p.Windows} {p.Edition}") })))));
  if (p.Options.Contains("bypass-hardware")) setup.Add(E("RunSynchronous", new[] { "TPM", "SecureBoot", "RAM" }.Select((n,i) => Command(i+1, $"reg.exe add HKLM\\SYSTEM\\Setup\\LabConfig /v Bypass{n}Check /t REG_DWORD /d 1 /f"))));
  if (setup.HasElements) pe.Add(setup);
  if (p.Language != "ISO default")
  {
   var peLocale = Component("Microsoft-Windows-International-Core-WinPE", E("SetupUILanguage", E("UILanguage", p.Language)));
   if (p.DesktopReady) { foreach(var key in new[] {"UILanguage","SystemLocale","UserLocale"}) peLocale.Add(E(key,p.Language)); peLocale.Add(E("InputLocale",KeyboardCode(p.Keyboard))); }
   pe.Add(peLocale);
  }
  if (pe.HasElements) root.Add(pe);
  var specialize = E("settings", new XAttribute("pass", "specialize"));
  var shell = Component("Microsoft-Windows-Shell-Setup");
  shell.Add(E("ComputerName", p.ComputerName.Length > 0 ? p.ComputerName : "*"));
  if (p.TimeZone != "Default") shell.Add(E("TimeZone", p.TimeZone));
  if (shell.HasElements) specialize.Add(shell);
  var scripts = BuildScript(p);
  var activateWindows = p.Options.Contains("activate-windows") ? File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Scripts", "activateWindows.cmd")).ReplaceLineEndings("\r\n").TrimEnd('\r', '\n') + "\r\n\r\n" : null;
  using var provisionerStream = typeof(AnswerFile).Assembly.GetManifestResourceStream("WinProvision.Scripts.Provisioner.ps1") ?? throw new InvalidOperationException(L.T("Missing app picker resource."));
  using var provisionerReader = new StreamReader(provisionerStream);
  var provisioner = provisionerReader.ReadToEnd();
  var appPlan=System.Text.Json.JsonSerializer.Serialize(new {Mode=p.AppSelectionMode,Ids=p.AppSelectionMode=="BeforeBoot"?p.SelectedApps.ToArray():Array.Empty<string>(),InterfaceLanguage=p.InterfaceLanguage});
  provisioner=provisioner.Replace("$initialPlan=@{Mode='AfterBoot';Ids=@()}","$initialPlan=ConvertFrom-Json @'\r\n"+appPlan+"\r\n'@");
  string? cleanup = null;
  var extract = """
    $ErrorActionPreference='Stop'
    $bootstrapLog=Join-Path $env:WINDIR 'Temp\WinProvision-Bootstrap.log'
    try {
    $x=[xml](Get-Content -LiteralPath ($env:WINDIR+'\Panther\unattend.xml') -Raw -Encoding UTF8)
    $d=$env:WINDIR+'\Setup\Scripts\WinProvision'
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    foreach($entry in @(@('Script','Configure.ps1'),@('ClassicMenu','ConfigureMenu.ps1'),@('ActivationPrompt','ActivationPrompt.ps1'),@('Launcher','LaunchApps.ps1'),@('Provisioner','Provisioner.ps1'),@('Localization','Translations.json'),@('LocalizationScript','Localization.ps1'),@('ActivateWindows','activateWindows.cmd'))) {
     $s=$x.SelectSingleNode('//*[local-name()="'+$entry[0]+'" and namespace-uri()="https://winprovision.local/schema/1"]')
     if($s) {
      $text=$s.InnerText
      if($s.GetAttribute('encoding') -eq 'base64') { $text=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($text)) }
      [IO.File]::WriteAllText((Join-Path $d $entry[1]),$text,[Text.UTF8Encoding]::new($entry[1].EndsWith('.ps1')))
     }
     elseif($entry[0] -eq 'Provisioner') { throw 'Missing app picker' }
    }
    $launchTarget=$env:WINDIR+'\System32\wscript.exe'
    $launchArguments='//B //NoLogo "'+$d+'\LaunchApps.js"'
    # A GUI host starts PowerShell hidden before a console can be attached.
    $js='var s=new ActiveXObject("WScript.Shell"); var f=new ActiveXObject("Scripting.FileSystemObject"); var p=f.BuildPath(f.GetParentFolderName(WScript.ScriptFullName),"LaunchApps.ps1"); s.Run(''"''+s.ExpandEnvironmentStrings("%SystemRoot%")+''\\System32\\WindowsPowerShell\\v1.0\\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "''+p+''"'',0,false);'
    [IO.File]::WriteAllText((Join-Path $d 'LaunchApps.js'),$js,[Text.UTF8Encoding]::new($false))
    if(Test-Path -LiteralPath (Join-Path $d 'ActivationPrompt.ps1')) {
     [IO.File]::WriteAllText((Join-Path $d 'ActivateWindows.js'),$js.Replace('LaunchApps.ps1','ActivationPrompt.ps1'),[Text.UTF8Encoding]::new($false))
     try {
      $activationShortcut=(New-Object -ComObject WScript.Shell).CreateShortcut(($env:PUBLIC+'\Desktop\WinProvision Activation.lnk'))
      $activationShortcut.TargetPath=$launchTarget
      $activationShortcut.Arguments='//B //NoLogo "'+$d+'\ActivateWindows.js"'
      $activationShortcut.Description='Confirm before opening Windows activation'
      $activationShortcut.Save()
     } catch { ('Activation shortcut: '+$_.Exception.Message) | Add-Content -LiteralPath $bootstrapLog }
    }
    # Shortcut creation must not prevent configuration or first-login registration.
    try {
     $shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut(($env:PUBLIC+'\Desktop\WinProvision Apps.lnk'))
     $shortcut.TargetPath=$launchTarget
     $shortcut.Arguments=$launchArguments
     $shortcut.WindowStyle=7
     $shortcut.Description='Choose and install your apps'
     $shortcut.Save()
    } catch { ('Shortcut: '+$_.Exception.Message) | Add-Content -LiteralPath $bootstrapLog }
    # Explorer runs HKCU RunOnce in the user's normal session, without inheriting Setup elevation.
    & reg.exe load HKU\WinProvisionAppsDefault ($env:SystemDrive+'\Users\Default\NTUSER.DAT') | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Cannot prepare first-login app picker'}
    try {
     $run='Registry::HKEY_USERS\WinProvisionAppsDefault\Software\Microsoft\Windows\CurrentVersion\RunOnce'
     New-Item -Path $run -Force | Out-Null
     $launch='"'+$launchTarget+'" '+$launchArguments
     New-ItemProperty -Path $run -Name WinProvisionApps -PropertyType String -Value $launch -Force | Out-Null
    } finally {
     [GC]::Collect(); [GC]::WaitForPendingFinalizers()
     & reg.exe unload HKU\WinProvisionAppsDefault | Out-Null
     if($LASTEXITCODE -ne 0){throw 'Cannot unload app picker default-user hive'}
    }
    if(Test-Path -LiteralPath ($d+'\Configure.ps1')) { & ($d+'\Configure.ps1') }
    } catch { $_.Exception.ToString() | Set-Content -LiteralPath $bootstrapLog -Encoding UTF8; throw }
    """;
  specialize.Add(Component("Microsoft-Windows-Deployment", E("RunSynchronous", Command(1, EmbeddedCommand("Bootstrap")))));
  if (specialize.HasElements) root.Add(specialize);
  var oobe = E("settings", new XAttribute("pass", "oobeSystem"));
  if (p.Language != "ISO default" || p.Keyboard != "Default")
  {
   var locale = Component("Microsoft-Windows-International-Core");
   if (p.Language != "ISO default") foreach (var key in new[] { "UILanguage", "SystemLocale", "UserLocale" }) locale.Add(E(key, p.Language));
   if (p.Keyboard != "Default") locale.Add(E("InputLocale", KeyboardCode(p.Keyboard)));
   oobe.Add(locale);
  }
  var accountShell = Component("Microsoft-Windows-Shell-Setup");
  if (p.Username.Length > 0)
  {
   var secret = preview ? "[PASSWORD OMITTED FROM PREVIEW]" : password;
   accountShell.Add(E("UserAccounts", E("LocalAccounts", E("LocalAccount", new object[] { new XAttribute(Wcm + "action", "add"), E("Name", p.Username), E("Group", p.Administrator ? "Administrators" : "Users"), Password(secret) }))));
   if (p.AutoLogon) accountShell.Add(E("AutoLogon", new object[] { E("Username", p.Username), E("Enabled", "true"), E("LogonCount", 1), Password(secret) }));
  }
  var oobeOptions = E("OOBE");
  if (p.DesktopReady) { oobeOptions.Add(E("HideEULAPage","true")); oobeOptions.Add(E("HideOEMRegistrationScreen","true")); }
  foreach (var (id,name,value) in new[] { ("hide-online","HideOnlineAccountScreens","true"), ("hide-wifi","HideWirelessSetupInOOBE","true"), ("privacy-oobe","ProtectYourPC","3") })
   if (p.Options.Contains(id)) oobeOptions.Add(E(name,value));
  if (oobeOptions.HasElements) accountShell.Add(oobeOptions);
  {
   var firstLogonCommands = new List<object>();
   string clean = "$ErrorActionPreference='Continue'; " + (p.AutoLogon ? "Set-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon' -Name AutoLogonCount -Value 0; Set-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon' -Name AutoAdminLogon -Value '0'; Remove-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon' -Name DefaultPassword -ErrorAction SilentlyContinue; " : "") + "foreach($n in @('unattend.xml','unattend-original.xml')){Remove-Item -LiteralPath ($env:WINDIR+'\\Panther\\'+$n) -Force -ErrorAction SilentlyContinue}";
   cleanup = (p.Options.Contains("classic-menu") ? "& ($env:WINDIR+'\\Setup\\Scripts\\WinProvision\\ConfigureMenu.ps1'); " : "") + clean;
   firstLogonCommands.Add(E("SynchronousCommand", new object[] { new XAttribute(Wcm+"action","add"), E("Order",1), E("Description","Clean up WinProvision setup files"), E("CommandLine", EmbeddedCommand("Cleanup")) }));
   accountShell.Add(E("FirstLogonCommands", firstLogonCommands));
  }
  if (accountShell.HasElements) oobe.Add(accountShell);
  if (oobe.HasElements) root.Add(oobe);
  using var localizationStream=typeof(L).Assembly.GetManifestResourceStream("WinProvision.Scripts.Localization.ps1")!;
  using var localizationReader=new StreamReader(localizationStream);
  var launcher = "$ErrorActionPreference='Continue'\r\n";
  if(activateWindows != null) launcher += """
    $state=Join-Path $env:LOCALAPPDATA 'WinProvision'
    $started=Join-Path $state 'activation-prompt-started.flag'
    try {
     New-Item -ItemType Directory -Path $state -Force -ErrorAction Stop | Out-Null
     if(-not (Test-Path -LiteralPath $started)) {
      $prompt=Join-Path $PSScriptRoot 'ActivateWindows.js'
      if(-not (Test-Path -LiteralPath $prompt)){throw 'Activation prompt is missing.'}
      Start-Process -FilePath ($env:WINDIR+'\System32\wscript.exe') -ArgumentList ('//B //NoLogo "'+$prompt+'"') -WindowStyle Hidden -ErrorAction Stop
      # This records opening the prompt only. Confirmation happens in its separate window.
      New-Item -ItemType File -Path $started -Force -ErrorAction Stop | Out-Null
     }
    } catch { $_.Exception.Message | Out-File (Join-Path $state 'Activation-launch.log') -Append -Encoding UTF8 -ErrorAction SilentlyContinue }

    """;
  launcher += "\r\n& (Join-Path $PSScriptRoot 'Provisioner.ps1')";
  using var activationPromptStream=typeof(AnswerFile).Assembly.GetManifestResourceStream("WinProvision.Scripts.ActivationPrompt.ps1")!;
  using var activationPromptReader=new StreamReader(activationPromptStream);
  var activationPrompt=activationPromptReader.ReadToEnd().Replace("$UiLanguage='en-US'", "$UiLanguage='"+p.InterfaceLanguage+"'");
  root.Add(new XElement(Ext+"Extensions",
    new XElement(Ext+"Bootstrap", Convert.ToBase64String(Encoding.UTF8.GetBytes(extract))),
    cleanup == null ? null : new XElement(Ext+"Cleanup", Convert.ToBase64String(Encoding.UTF8.GetBytes(cleanup))),
    scripts == null ? null : Payload("Script", scripts),
    p.Options.Contains("classic-menu") ? Payload("ClassicMenu", ClassicMenuScript) : null,
    Payload("Launcher", launcher),
    activateWindows == null ? null : Payload("ActivationPrompt", activationPrompt),
    Payload("Provisioner", provisioner),
    Payload("Localization", L.Json),
    Payload("LocalizationScript", localizationReader.ReadToEnd()),
    activateWindows == null ? null : Payload("ActivateWindows", activateWindows)));
  return new XDocument(new XDeclaration("1.0", "utf-8", null), new XComment("Generated by WinProvision. Disk selection is interactive. Apps are selected after first logon."), root).ToString();
 }
 static string KeyboardCode(string keyboard) => keyboard switch { "US" => "0409:00000409", "Brazil ABNT2" => "0416:00010416", _ => "040a:0000040a" };
 const string ClassicMenuScript = """
  $ErrorActionPreference='Stop'
  $state=Join-Path $env:LOCALAPPDATA 'WinProvision'
  New-Item -ItemType Directory -Path $state -Force | Out-Null
  try {
   # Software\Classes belongs to the actual user's classes hive, not Default NTUSER.DAT.
   $key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32')
   try {
    $key.SetValue('','',[Microsoft.Win32.RegistryValueKind]::String)
    if($key.GetValueNames() -notcontains '' -or $key.GetValue('') -ne ''){throw 'Classic menu registry verification failed.'}
   } finally {if($key){$key.Dispose()}}
   'OK: classic-menu' | Set-Content (Join-Path $state 'ClassicMenu.log') -Encoding UTF8
  } catch {
   ('FAILED: classic-menu: '+$_.Exception.Message) | Set-Content (Join-Path $state 'ClassicMenu.log') -Encoding UTF8
   throw
  }
  """;
 static string? BuildScript(BuildProfile p)
 {
  var tweaks = Catalog.All.Where(o => p.Options.Contains(o.Id) && o.Id != "classic-menu" && o.Scope is "System" or "User").ToArray();
  if (tweaks.Length == 0) return null;
  var b = new StringBuilder("$ErrorActionPreference='Stop'\r\n$log=Join-Path $env:WINDIR 'Setup\\Scripts\\WinProvision\\Configure.log'\r\nfunction Set-Reg($path,$name,$type,$value) {\r\n $p='Registry::'+($path -replace '^HKLM', 'HKEY_LOCAL_MACHINE' -replace '^HKCU', 'HKEY_CURRENT_USER' -replace '^HKU', 'HKEY_USERS');\r\n if(-not (Test-Path -LiteralPath $p)) { New-Item -Path $p -Force | Out-Null };\r\n if($name -eq '') { Set-Item -LiteralPath $p -Value $value } else { New-ItemProperty -LiteralPath $p -Name $name -PropertyType $type -Value $value -Force | Out-Null }\r\n}\r\n");
  var hasUsers=tweaks.Any(t => t.Scope == "User");
  if(hasUsers) b.AppendLine("& reg.exe load HKU\\WinProvisionDefault ($env:SystemDrive+'\\Users\\Default\\NTUSER.DAT') | Out-Null\nif($LASTEXITCODE -ne 0){throw 'Cannot load default-user profile'}\ntry {");
  foreach (var t in tweaks)
  {
   var code = t.Scope == "User" ? t.Code.Replace("HKCU\\", "HKU\\WinProvisionDefault\\") : t.Code;
   b.AppendLine($"try {{\n{code}\n Add-Content -LiteralPath $log -Value 'OK: {t.Id}'\n}} catch {{ $failed=$true; Add-Content -LiteralPath $log -Value ('FAILED: {t.Id}: '+$_.Exception.Message) }}");
  }
  if(hasUsers) b.AppendLine("} finally { [GC]::Collect(); [GC]::WaitForPendingFinalizers(); & reg.exe unload HKU\\WinProvisionDefault | Out-Null; if($LASTEXITCODE -ne 0){throw 'Cannot unload default-user profile'} }");
  return b.ToString();
 }
}
