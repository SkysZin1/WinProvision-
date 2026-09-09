using System.Text;
using System.IO;
using System.Xml.Linq;

namespace WinProvision;
public static class AnswerFile
{
 static readonly XNamespace Ns = "urn:schemas-microsoft-com:unattend";
 static readonly XNamespace Wcm = "http://schemas.microsoft.com/WMIConfig/2002/State";
 static readonly XNamespace Ext = "https://winprovision.local/schema/1";
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
   throw new ArgumentException("Automatic desktop setup requires a local administrator account, one-time logon, explicit language, keyboard and time zone, and the three OOBE options. Complete the Recommended setup details before continuing.");
  if (p.Username.Length > 0 && password.Length == 0 && !preview) throw new ArgumentException("Set a password for the local account, or leave the account name empty to create it during Windows Setup.");
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
  var activateWindows = p.Options.Contains("activate-windows") ? File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Scripts", "activateWindows.cmd")) : null;
  using var provisionerStream = typeof(AnswerFile).Assembly.GetManifestResourceStream("WinProvision.Scripts.Provisioner.ps1") ?? throw new InvalidOperationException("Missing app picker resource.");
  using var provisionerReader = new StreamReader(provisionerStream);
  var provisioner = provisionerReader.ReadToEnd();
  var appPlan=System.Text.Json.JsonSerializer.Serialize(new {Mode=p.AppSelectionMode,Ids=p.AppSelectionMode=="BeforeBoot"?p.SelectedApps.ToArray():Array.Empty<string>()});
  provisioner=provisioner.Replace("$initialPlan=@{Mode='AfterBoot';Ids=@()}","$initialPlan=ConvertFrom-Json @'\r\n"+appPlan+"\r\n'@");
  {
   var extract = """
    $ErrorActionPreference='Stop'
    $x=[xml](Get-Content -LiteralPath ($env:WINDIR+'\Panther\unattend.xml') -Raw)
    $d=$env:WINDIR+'\Setup\Scripts\WinProvision'
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    foreach($entry in @(@('Script','Configure.ps1'),@('Provisioner','Provisioner.ps1'),@('ActivateWindows','activateWindows.cmd'))) {
     $s=$x.SelectSingleNode('//*[local-name()="'+$entry[0]+'" and namespace-uri()="https://winprovision.local/schema/1"]')
     if($s) { [IO.File]::WriteAllText((Join-Path $d $entry[1]),$s.InnerText,[Text.UTF8Encoding]::new($true)) }
     elseif($entry[0] -eq 'Provisioner') { throw 'Missing app picker' }
    }
    $shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut(($env:PUBLIC+'\Desktop\WinProvision Apps.lnk'))
    $shortcut.TargetPath=$env:WINDIR+'\System32\WindowsPowerShell\v1.0\powershell.exe'
    $shortcut.Arguments='-NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+$d+'\Provisioner.ps1"'
    $shortcut.WindowStyle=7
    $shortcut.Description='Choose and install your apps'
    $shortcut.Save()
    # Explorer runs HKCU RunOnce in the user's normal session, without inheriting Setup elevation.
    & reg.exe load HKU\WinProvisionAppsDefault ($env:SystemDrive+'\Users\Default\NTUSER.DAT') | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Cannot prepare first-login app picker'}
    try {
     $run='Registry::HKEY_USERS\WinProvisionAppsDefault\Software\Microsoft\Windows\CurrentVersion\RunOnce'
     New-Item -Path $run -Force | Out-Null
     $launch='"'+$shortcut.TargetPath+'" '+$shortcut.Arguments
     New-ItemProperty -Path $run -Name WinProvisionApps -PropertyType String -Value $launch -Force | Out-Null
    } finally {
     [GC]::Collect(); [GC]::WaitForPendingFinalizers()
     & reg.exe unload HKU\WinProvisionAppsDefault | Out-Null
     if($LASTEXITCODE -ne 0){throw 'Cannot unload app picker default-user hive'}
    }
    if(Test-Path -LiteralPath ($d+'\Configure.ps1')) { & ($d+'\Configure.ps1') }
    """;
   string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(extract));
   specialize.Add(Component("Microsoft-Windows-Deployment", E("RunSynchronous", Command(1, "powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded))));
  }
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
   firstLogonCommands.Add(E("SynchronousCommand", new object[] { new XAttribute(Wcm+"action","add"), E("Order",1), E("Description","Clean up WinProvision setup files"), E("CommandLine", "powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(clean))) }));
   if (p.Options.Contains("activate-windows"))
   {
    var cmd = "cmd.exe /c \"\"C:\\Windows\\Setup\\Scripts\\WinProvision\\activateWindows.cmd\"\"";
    firstLogonCommands.Add(E("SynchronousCommand", new object[] { new XAttribute(Wcm+"action","add"), E("Order",2), E("Description","Activate Windows on first login"), E("CommandLine", cmd) }));
   }
   accountShell.Add(E("FirstLogonCommands", firstLogonCommands));
  }
  if (accountShell.HasElements) oobe.Add(accountShell);
  if (oobe.HasElements) root.Add(oobe);
  root.Add(new XElement(Ext+"Extensions",
    scripts == null ? null : new XElement(Ext+"Script", scripts),
    new XElement(Ext+"Provisioner", provisioner),
    activateWindows == null ? null : new XElement(Ext+"ActivateWindows", activateWindows)));
  return new XDocument(new XDeclaration("1.0", "utf-8", null), new XComment("Generated by WinProvision. Disk selection is interactive. Apps are selected after first logon."), root).ToString();
 }
 static string KeyboardCode(string keyboard) => keyboard switch { "US" => "0409:00000409", "Brazil ABNT2" => "0416:00010416", _ => "040a:0000040a" };
 static string? BuildScript(BuildProfile p)
 {
  var tweaks = Catalog.All.Where(o => p.Options.Contains(o.Id) && o.Scope is "System" or "User").ToArray();
  if (tweaks.Length == 0) return null;
  var b = new StringBuilder("$ErrorActionPreference='Stop'\r\n$log=Join-Path $env:WINDIR 'Setup\\Scripts\\WinProvision\\Configure.log'\r\nfunction Set-Reg($path,$name,$type,$value) {\r\n $p='Registry::'+($path -replace '^HKLM', 'HKEY_LOCAL_MACHINE' -replace '^HKCU', 'HKEY_CURRENT_USER' -replace '^HKU', 'HKEY_USERS');\r\n New-Item -Path $p -Force | Out-Null;\r\n if($name -eq '') { Set-Item -LiteralPath $p -Value $value } else { New-ItemProperty -LiteralPath $p -Name $name -PropertyType $type -Value $value -Force | Out-Null }\r\n}\r\n");
  var users = tweaks.Where(o => o.Scope == "User").ToArray();
  if (users.Length > 0) b.AppendLine("$hiveLoaded=$false\ntry {\n & reg.exe load HKU\\WinProvisionDefault ($env:SystemDrive+'\\Users\\Default\\NTUSER.DAT') | Out-Null\n if($LASTEXITCODE -ne 0){throw 'Cannot load default-user registry'}\n $hiveLoaded=$true");
  foreach (var t in tweaks)
  {
   var code = t.Scope == "User" ? t.Code.Replace("HKCU\\", "HKU\\WinProvisionDefault\\") : t.Code;
   b.AppendLine($"try {{\n{code}\n Add-Content -LiteralPath $log -Value 'OK: {t.Id}'\n}} catch {{ Add-Content -LiteralPath $log -Value ('FAILED: {t.Id}: '+$_.Exception.Message) }}");
  }
  if (users.Length > 0) b.AppendLine("} finally { if($hiveLoaded) { [GC]::Collect(); [GC]::WaitForPendingFinalizers(); & reg.exe unload HKU\\WinProvisionDefault | Out-Null } }");
  return b.ToString();
 }
}
