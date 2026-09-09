namespace WinProvision;
public static class Catalog
{
 const string Both = "Windows 10 & 11 · effect may vary by edition/build";
 const string Eleven = "Windows 11 only · availability depends on build";
 const string Ten = "Windows 10 only";
 // Safe example command for the optional setup hook. Replace this value only
 // with a reviewed, legitimate command; it is embedded in the generated XML.
 const string SetupCommandHook = "& cmd.exe /c \"echo WinProvision setup hook completed > %WINDIR%\\Setup\\Scripts\\WinProvision\\CommandHook.log\"";
 static Tweak Reg(string id, string cat, string title, string desc, string path, string name, string value, string type = "DWord", string compat = Both, bool advanced = false)
 {
  var scope = path.StartsWith("HKCU") ? "User" : "System";
  return new(id, cat, title, desc, compat, scope, $"Set-Reg '{path}' '{name}' '{type}' '{value}'", advanced);
 }
 public static readonly List<Tweak> All =
 [
  new("hide-online", "Setup", "Hide Microsoft account screens", "Use with a configured local account. OOBE behavior varies between Windows builds; this is not a universal offline bypass.", Both, "Xml", ""),
  new("hide-wifi", "Setup", "Hide wireless setup screen", "Suppress the wireless setup page where supported. Does not install network drivers or guarantee offline setup.", Both, "Xml", ""),
  new("privacy-oobe", "Setup", "Skip express privacy choices", "Set ProtectYourPC to 3. This does not disable all Windows telemetry.", Both, "Xml", ""),
  new("setup-command-hook", "Setup", "Run setup command hook", "Run the reviewed command defined in Catalog.cs during setup. The example only writes a log file; edit the marked constant for a legitimate deployment command.", Both, "System", SetupCommandHook, true),
  new("bypass-hardware", "Setup", "Bypass hardware checks", "Request TPM, Secure Boot and RAM check bypasses. Unsupported installation; newer builds may ignore these registry settings.", Eleven, "PE", "", true),
  Reg("extensions", "Explorer", "Show file extensions", "Display extensions such as .txt and .exe for known file types.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "HideFileExt", "0"),
  Reg("hidden", "Explorer", "Show hidden files", "Show hidden files while retaining Windows' protected-system-file setting.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "Hidden", "1"),
  Reg("protected", "Explorer", "Show protected system files", "Expose files normally hidden to protect the operating system.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowSuperHidden", "1", advanced:true),
  Reg("this-pc", "Explorer", "Open Explorer to This PC", "Start File Explorer at your drives instead of Home / Quick access.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "LaunchTo", "1"),
  Reg("classic-menu", "Explorer", "Classic context menu", "Request the classic right-click menu. May change in newer Windows 11 builds.", "HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32", "", "", "String", Eleven),
  Reg("no-tooltips", "Explorer", "Hide folder tooltips", "Turn off pop-up descriptions for folder and desktop items.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowInfoTip", "0"),
  Reg("search-icon", "Start & taskbar", "Compact search icon", "Use a search icon instead of the full search box.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Search", "SearchboxTaskbarMode", "1"),
  Reg("task-left", "Start & taskbar", "Left-align taskbar", "Align Start and taskbar icons to the left.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarAl", "0", compat:Eleven),
  Reg("no-taskview", "Start & taskbar", "Hide Task View", "Remove the Task View button from the taskbar.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowTaskViewButton", "0"),
  Reg("no-widgets", "Start & taskbar", "Disable widgets", "Disable the Windows 11 widgets panel using its policy setting.", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Dsh", "AllowNewsAndInterests", "0", compat:Eleven),
  Reg("no-news", "Start & taskbar", "Hide News and interests", "Hide the Windows 10 taskbar news and weather feed.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Feeds", "ShellFeedsTaskbarViewMode", "2", compat:Ten),
  Reg("no-bing", "Start & taskbar", "Disable web search suggestions", "Request local search without Bing suggestions. Behavior varies by Windows build.", "HKCU\\Software\\Policies\\Microsoft\\Windows\\Explorer", "DisableSearchBoxSuggestions", "1"),
  Reg("end-task", "Start & taskbar", "Show End task", "Add End task to taskbar app menus on Windows 11 23H2 or later.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\\TaskbarDeveloperSettings", "TaskbarEndTask", "1", compat:"Windows 11 only · 23H2 or later"),
  Reg("dark-apps", "Appearance", "Dark app theme", "Use dark mode for applications that follow Windows theme preferences.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "AppsUseLightTheme", "0"),
  Reg("dark-system", "Appearance", "Dark Windows theme", "Use a dark Start menu and taskbar. Windows 10 1903 or later.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "SystemUsesLightTheme", "0"),
  Reg("no-transparency", "Appearance", "Disable transparency", "Use opaque Windows surfaces.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "EnableTransparency", "0"),
  Reg("desktop-pc", "Appearance", "This PC on desktop", "Show the This PC icon on the desktop.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\HideDesktopIcons\\NewStartPanel", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "0"),
  Reg("desktop-bin", "Appearance", "Recycle Bin on desktop", "Show the Recycle Bin icon on the desktop.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\HideDesktopIcons\\NewStartPanel", "{645FF040-5081-101B-9F08-00AA002F954E}", "0"),
  Reg("no-min-animation", "Appearance", "Disable window minimize animation", "Reduce window animation. Applies after sign-out or Explorer restart.", "HKCU\\Control Panel\\Desktop\\WindowMetrics", "MinAnimate", "0", "String"),
  Reg("long-paths", "System", "Enable long paths", "Enable long-path support for applications that opt in.", "HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem", "LongPathsEnabled", "1"),
  Reg("no-fast-start", "System", "Disable Fast Startup", "Use a full shutdown rather than a hybrid shutdown.", "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power", "HiberbootEnabled", "0"),
  new("no-hibernate", "System", "Disable hibernation", "Remove hibernation support and its disk file. Also affects Fast Startup.", Both, "System", "& powercfg.exe /hibernate off; if ($LASTEXITCODE -ne 0) { throw 'powercfg failed' }", true),
  new("high-performance", "System", "High performance power plan", "Select the built-in High performance plan when hardware supports it. Can increase power use; Modern Standby devices may reject it.", Both, "System", "& powercfg.exe /setactive SCHEME_MIN; if ($LASTEXITCODE -ne 0) { throw 'Power plan unavailable on this device' }"),
  new("remote-signed", "System", "Allow local PowerShell scripts", "Set LocalMachine execution policy to RemoteSigned. Domain policy can override this setting.", Both, "System", "Set-ExecutionPolicy -Scope LocalMachine -ExecutionPolicy RemoteSigned -Force", true),
  Reg("no-suggestions", "Privacy & security", "Disable suggested app delivery", "Request that Windows does not provision promotional apps. Policy support varies by edition; may affect connected-device features.", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent", "DisableWindowsConsumerFeatures", "1"),
  Reg("no-ad-id", "Privacy & security", "Disable advertising ID", "Turn off the user advertising identifier.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo", "Enabled", "0"),
  Reg("no-tailored", "Privacy & security", "Disable tailored experiences", "Turn off personalized tips based on diagnostic data.", "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", "0"),
  Reg("no-encryption", "Privacy & security", "Prevent automatic device encryption", "Request that Windows does not automatically encrypt this installation. Does not decrypt an existing drive.", "HKLM\\SYSTEM\\CurrentControlSet\\Control\\BitLocker", "PreventDeviceEncryption", "1", advanced:true),
  Reg("no-autorun", "Privacy & security", "Disable AutoRun", "Disable AutoRun across drive types.", "HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer", "NoDriveTypeAutoRun", "255"),
  Reg("no-assistance", "Privacy & security", "Disable Remote Assistance", "Prevent Remote Assistance invitations. This is separate from Remote Desktop.", "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Remote Assistance", "fAllowToGetHelp", "0"),
  new("allow-ping", "Network", "Allow inbound ping", "Add firewall rules for ICMP echo requests on Private networks only.", Both, "System", "New-NetFirewallRule -Name 'WinProvision-Ping4' -DisplayName 'WinProvision: Ping (IPv4)' -Profile Private -Protocol ICMPv4 -IcmpType 8 -Direction Inbound -Action Allow | Out-Null\nNew-NetFirewallRule -Name 'WinProvision-Ping6' -DisplayName 'WinProvision: Ping (IPv6)' -Profile Private -Protocol ICMPv6 -IcmpType 128 -Direction Inbound -Action Allow | Out-Null"),
  Reg("no-edge-first", "Edge", "Skip Edge welcome screens", "Hide the first-run experience in Microsoft Edge.", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge", "HideFirstRunExperience", "1"),
  Reg("no-edge-boost", "Edge", "Disable Edge Startup Boost", "Stop Edge from preloading to speed up launch.", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge", "StartupBoostEnabled", "0"),
  Reg("no-edge-background", "Edge", "Disable Edge background mode", "Stop Edge background apps after the browser closes.", "HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge", "BackgroundModeEnabled", "0"),
  Reg("numlock", "Accessibility", "Enable Num Lock at sign-in", "Set the initial Num Lock state on the sign-in screen. Firmware may override it.", "HKU\\.DEFAULT\\Control Panel\\Keyboard", "InitialKeyboardIndicators", "2", "String"),
  Reg("no-sticky", "Accessibility", "Disable Sticky Keys shortcut", "Turn off Sticky Keys and the five-Shift shortcut for configured users.", "HKCU\\Control Panel\\Accessibility\\StickyKeys", "Flags", "506", "String"),
  new("no-acceleration", "Accessibility", "Disable mouse acceleration", "Turn off Enhance pointer precision for configured users.", Both, "User", "Set-Reg 'HKCU\\Control Panel\\Mouse' 'MouseSpeed' 'String' '0'\nSet-Reg 'HKCU\\Control Panel\\Mouse' 'MouseThreshold1' 'String' '0'\nSet-Reg 'HKCU\\Control Panel\\Mouse' 'MouseThreshold2' 'String' '0'")
 ];
 static Catalog()
 {
  foreach (var (id, title, package, compat) in new[] {
   ("clipchamp","Clipchamp","Clipchamp.Clipchamp",Eleven), ("solitaire","Solitaire Collection","Microsoft.MicrosoftSolitaireCollection",Both),
   ("news","Microsoft News","Microsoft.BingNews",Both), ("weather","Weather","Microsoft.BingWeather",Both),
   ("gethelp","Get Help","Microsoft.GetHelp",Both), ("feedback","Feedback Hub","Microsoft.WindowsFeedbackHub",Both),
   ("phone","Phone Link","Microsoft.YourPhone",Both), ("todos","Microsoft To Do","Microsoft.Todos",Both),
   ("maps","Maps","Microsoft.WindowsMaps",Both), ("outlook","Outlook for Windows","Microsoft.OutlookForWindows",Both),
   ("teams","Teams (MSIX)","MSTeams",Both), ("copilot","Copilot (app)","Microsoft.Copilot",Eleven)
  }) All.Add(new("remove-" + id, "Remove built-in apps", "Remove " + title, "Remove the provisioned " + title + " package if present. Does not install replacements; availability varies by image.", compat, "System", $"Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq '{package}' | Remove-AppxProvisionedPackage -Online | Out-Null"));
 }
}
