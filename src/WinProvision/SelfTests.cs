using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace WinProvision;
public static class SelfTests
{
 static string ReadPayload(XDocument doc, string name) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(doc.Descendants().Single(e=>e.Name.LocalName==name).Value));
 internal static void RenderInterface(System.Windows.FrameworkElement content,string name,int width,int height)
 {
  var background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243,246,245));
  if(content is System.Windows.Controls.Panel panel)panel.Background=background;
  if(content is System.Windows.Controls.Control control)control.Background=background;
  content.Measure(new System.Windows.Size(width,height));
  content.Arrange(new System.Windows.Rect(0,0,width,height));content.UpdateLayout();
  var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32);
  bitmap.Render(content);
  var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
  var folder=Path.Combine(AppContext.BaseDirectory,"ui-validation");Directory.CreateDirectory(folder);
  using var file=File.Create(Path.Combine(folder,name+".png"));encoder.Save(file);
 }
 public static void Run()
 {
  int passed=0;
  void Check(bool result,string message){if(!result)throw new Exception(message);passed++;}
  void Reject(Action action,string message){try{action();}catch(ArgumentException){passed++;return;}catch(InvalidDataException){passed++;return;}throw new Exception(message);}
  var originalLanguage=L.Language;
  foreach(var language in new[]{"pt-BR","en-US"})
  {
   L.SetLanguage(language);
   Check(L.T("Recommended")==(language=="pt-BR"?"Recomendado":"Recommended"),"Preset translation failed.");
   Check(L.F($"ERASE DISK {99}")==(language=="pt-BR"?"APAGAR DISCO 99":"ERASE DISK 99"),"Typed confirmation translation failed.");
   Check(L.F($"{2} app(s) selected.")==(language=="pt-BR"?"2 aplicativo(s) selecionado(s).":"2 app(s) selected."),"Formatted translation failed.");
   var bilingual=new BuildProfile{InterfaceLanguage=language,Options=["extensions"]};
   var saved=JsonSerializer.Deserialize<BuildProfile>(JsonSerializer.Serialize(bilingual))!;
   Check(saved.InterfaceLanguage==language && saved.Options.SetEquals(bilingual.Options),"Language/profile round-trip failed.");
   Check(InstallationPresets.Create(InstallationPresets.Recommended,bilingual).InterfaceLanguage==language,"Preset lost interface language.");
   var xml=XDocument.Parse(AnswerFile.Generate(bilingual,""));
   Check(ReadPayload(xml, "Provisioner").Contains("\"InterfaceLanguage\":\""+language+"\""),"Picker language missing from XML.");
   Check(xml.Descendants().Any(e=>e.Name.LocalName=="Localization") && xml.Descendants().Any(e=>e.Name.LocalName=="LocalizationScript"),"Offline localization resources missing.");
   Check(Catalog.All.Single(o=>o.Id=="remove-weather").DisplayTitle==(language=="pt-BR"?"Remover Clima":"Remove Weather"),"Removal translation failed.");
   if(language=="pt-BR"){
    foreach(var option in Catalog.All){
     Check(option.DisplayTitle!=option.Title && option.DisplayDescription!=option.Description,"Missing option translation: "+option.Id);
     Check(option.DisplayCompatibility!=option.Compatibility,"Missing compatibility translation: "+option.Id);
    }
   }
  }
  Reject(()=>new BuildProfile{InterfaceLanguage="unknown"}.Validate(),"Invalid UI language accepted.");
  L.SetLanguage(originalLanguage);
  var defaultXml=AnswerFile.Generate(new BuildProfile(),"");
  Check(AppCatalog.Names.Count==36,"Expected the full 36-app catalog.");
  foreach(var id in new[]{"Opera.OperaGX","Zoom.Zoom","qBittorrent.qBittorrent","BlenderFoundation.Blender","AntibodySoftware.WizTree","Anysphere.Cursor"})
   Check(AppCatalog.Names.ContainsKey(id),"Missing new app: "+id);
  new BuildProfile{SelectedApps=AppCatalog.Names.Keys.ToHashSet(),AppSelectionMode="BeforeBoot"}.Validate();
  var appsProfile=new BuildProfile{AppSelectionMode="BeforeBoot",SelectedApps=["7zip.7zip","Mozilla.Firefox"]};
  var appsRoundTrip=JsonSerializer.Deserialize<BuildProfile>(JsonSerializer.Serialize(appsProfile))!;
  Check(appsRoundTrip.AppSelectionMode=="BeforeBoot" && appsRoundTrip.SelectedApps.SetEquals(appsProfile.SelectedApps),"Application selection must round-trip.");
  var plannedXml=XDocument.Parse(AnswerFile.Generate(appsProfile,""));
  var plannedScript=ReadPayload(plannedXml, "Provisioner");
  Check(plannedScript.Contains("\"Mode\":\"BeforeBoot\"") && plannedScript.Contains("\"Ids\":[\"7zip.7zip\",\"Mozilla.Firefox\"]"),"Automatic app plan missing from XML.");
  Check(!plannedScript.Contains("$initialPlan=@{Mode='AfterBoot';Ids=@()}"),"App plan placeholder was not replaced.");
  Check(InstallationPresets.Create(InstallationPresets.Recommended,appsProfile).SelectedApps.SetEquals(appsProfile.SelectedApps),"Windows presets must preserve app selections.");
  Reject(()=>new BuildProfile{AppSelectionMode="BeforeBoot"}.Validate(),"Empty automatic app selection accepted.");
  Reject(()=>new BuildProfile{SelectedApps=["unknown-app"]}.Validate(),"Unknown app accepted.");
  Reject(()=>new BuildProfile{AppSelectionMode="unexpected"}.Validate(),"Unknown app mode accepted.");
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"test-planned-apps.ps1"),plannedScript);
  XDocument.Parse(defaultXml);
  Check(XDocument.Parse(defaultXml).Descendants().Any(e=>e.Name.LocalName=="settings"),"The default answer file must contain a valid settings pass.");
  Check(!defaultXml.Contains("DiskConfiguration") && !defaultXml.Contains("InstallTo"),"Default XML must not automate target-disk erasure.");
  var defaultDoc=XDocument.Parse(defaultXml);
  Check(!defaultDoc.Descendants().Any(e=>e.Name.LocalName=="Script"),"Default XML should not include tweak scripts.");
  Check(defaultDoc.Descendants().Count(e=>e.Name.LocalName=="Provisioner")==1,"App picker must be embedded even without account customization.");
  Check(ReadPayload(defaultDoc, "Provisioner").Contains("if($Worker)"),"Embedded app picker must contain its worker.");
  var activationProfile = new BuildProfile { Username = "ActivateTest", AutoLogon = true, Language = "pt-BR", Keyboard = "Brazil ABNT2", Options = ["activate-windows"] };
  var activationXml = XDocument.Parse(AnswerFile.Generate(activationProfile, "Pass123!"));
  Check(activationXml.Descendants().Any(e=>e.Name.LocalName=="ActivateWindows"),"Activation script must be embedded in the generated XML.");
  Check(ReadPayload(activationXml,"ActivateWindows").EndsWith("\r\n\r\n"),"CMD payload must end with an empty CRLF line.");
  Check(ReadPayload(activationXml,"Bootstrap").Contains("wscript.exe") && ReadPayload(activationXml,"Bootstrap").Contains("0,false"),"Picker must start through a hidden GUI launcher.");
  Check(InstallationPresets.RecommendedPassword=="123","Recommended default password must match the displayed value.");
  Check(!activationXml.Descendants().Any(e=>e.Name.LocalName=="CommandLine" && e.Value.Contains("activateWindows.cmd")),"Activation must not run twice through FirstLogonCommands.");
  Check(ReadPayload(activationXml,"Launcher").Contains("ActivateWindows.js") && !ReadPayload(activationXml,"Launcher").Contains("/d /k") && !ReadPayload(activationXml,"Launcher").Contains("-Wait"),"Startup must open only the confirmation window without blocking App Picker.");
  Check(ReadPayload(activationXml,"ActivationPrompt").Contains("$confirmButton.Add_Click") && ReadPayload(activationXml,"ActivationPrompt").Contains("/d /k"),"Activation must remain interactive and require confirmation.");
  Check(!defaultDoc.Descendants().Any(e=>e.Name.LocalName=="ActivationPrompt"),"Activation prompt must remain opt-in.");
  Check(!ReadPayload(defaultDoc,"Launcher").Contains("activateWindows.cmd"),"Activation must remain opt-in.");
  foreach(var path in defaultDoc.Descendants().Where(e=>e.Name.LocalName=="Path"))
  {
   Check(path.Value.Length<=259,"Setup command must not exceed the Windows 259-character limit.");
   Check(path.Value.Contains("Extensions.Bootstrap"),"Specialize must load the embedded bootstrap.");
   var extraction=ReadPayload(defaultDoc,"Bootstrap");
   Check(extraction.Contains("WinProvisionAppsDefault") && extraction.Contains("RunOnce") && extraction.Contains("finally"),"App picker must launch once in the user session and unload its hive.");
   Check(!extraction.Contains("winget install") && !extraction.Contains("-Worker"),"Specialize must not start app installations.");
   File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"test-app-extraction.ps1"),extraction);
  }
  Check(Catalog.All.Select(o=>o.Id).Distinct().Count()==Catalog.All.Count,"Option IDs must be unique.");
  foreach(var os in new[]{"10","11"})
  {
   var profile=new BuildProfile {Windows=os,Username="Test & User",ComputerName="BUILD-PC",AutoLogon=true,Language="pt-BR",Keyboard="Brazil ABNT2",Edition="Pro",Options=Catalog.All.Where(o=>o.Supports(os)).Select(o=>o.Id).ToHashSet()};
   const string secret="Test<&'\"42";
   var xml=AnswerFile.Generate(profile,secret);var doc=XDocument.Parse(xml);
   Check(doc.Descendants().Where(e=>e.Name.LocalName=="Path").All(e=>e.Value.Length<=259),"All Setup paths must satisfy the Windows length limit with every supported option.");
   Check(doc.Descendants().Where(e=>e.Name.LocalName=="CommandLine").All(e=>e.Value.Length<=1024),"First-logon commands must satisfy the Windows length limit.");
   Check(doc.Descendants().Any(e=>e.Name.LocalName=="Value" && e.Value==secret),"Passwords must round-trip through XML escaping.");
   Check(!AnswerFile.Generate(profile,secret,true).Contains("Test&lt;"),"Preview must omit password.");
   Check(!JsonSerializer.Serialize(profile).Contains(secret),"Saved profiles must not include password.");
   Check(!doc.Descendants().Any(e=>e.Name.LocalName is "DiskConfiguration" or "InstallTo"),"No target-disk automation may be generated.");
   var script=ReadPayload(doc, "Script");
   if(profile.Options.Contains("classic-menu")) {
    Check(!script.Contains("86ca1aa0"),"Classic menu must not target Default NTUSER.DAT.");
    Check(ReadPayload(doc,"Cleanup").Contains("ConfigureMenu.ps1"),"Classic menu must run in the first-login account.");
    Check(ReadPayload(doc,"ClassicMenu").Contains("CurrentUser.CreateSubKey") && ReadPayload(doc,"ClassicMenu").Contains("GetValueNames()"),"Classic menu must verify the actual user value.");
   }
   Check(!script.Contains("Set-Reg 'HKCU"),"Setup preferences must target the default profile, not the setup account.");
   Check(script.Contains("if(-not (Test-Path -LiteralPath $p))"),"Existing registry keys must be preserved.");
   Check(script.Contains("WinProvisionDefault") && script.Contains("finally"),"Default user preferences must be prepared during setup and the hive unloaded.");
   var launcher=ReadPayload(doc,"Launcher");
   Check(!launcher.Contains("ConfigureUser.ps1") && launcher.Contains("Provisioner.ps1"),"Picker launch must not depend on first-login configuration.");
   Check(!script.Contains("winget") && !script.Contains("DownloadFile") && !script.Contains("Start-Process"),"Configuration scripts must not install third-party apps.");
   File.WriteAllText(Path.Combine(AppContext.BaseDirectory,$"test-generated-{os}.ps1"),script);
   File.WriteAllText(Path.Combine(AppContext.BaseDirectory,$"test-generated-{os}.xml"),xml);
  }
  Reject(()=>new BuildProfile {Windows="10",Options=["task-left"]}.Validate(),"Windows 11 option must be rejected on Windows 10.");
  Reject(()=>new BuildProfile {Options=["unknown"]}.Validate(),"Unknown options must be rejected.");
  Reject(()=>new BuildProfile {ComputerName="$(whoami)"}.Validate(),"Invalid computer name accepted.");
  Reject(()=>new BuildProfile {AutoLogon=true}.Validate(),"Autologon without an account accepted.");
  Reject(()=>AnswerFile.Generate(new BuildProfile {Username="Test"},""),"Empty local password accepted.");
  Reject(()=>new BuildProfile {Username="Administrator"}.Validate(),"Reserved local account accepted.");
  Reject(()=>new BuildProfile {SchemaVersion=9}.Validate(),"Unknown schema accepted.");
  var custom=new BuildProfile {Windows="10",Edition="Pro",Username="Example",ComputerName="MY-PC",Language="pt-BR",Options=["hidden","no-hibernate"]};
  foreach(var os in new[]{"10","11"})
  {
   custom.Windows=os;
   var standard=InstallationPresets.Create(InstallationPresets.Default,custom);
   Check(standard.Options.Count==0 && standard.Username=="" && standard.Language=="ISO default" && !standard.AutoLogon,"Default must remove custom settings from the active plan.");
   Check(standard.Windows==os && standard.Edition=="Pro","Preset must preserve the media selection.");
   Check(custom.Username=="Example" && custom.Options.Contains("no-hibernate"),"Creating a preset must not mutate the custom draft.");
   var recommended=InstallationPresets.Create(InstallationPresets.Recommended,custom);
   recommended.Validate();
   Check(recommended.Options.SetEquals(InstallationPresets.RecommendedOptions),"Recommended selection must contain the documented tweaks and OOBE settings.");
   Check(Catalog.All.Where(o=>recommended.Options.Contains(o.Id)).All(o=>o.Supports(os) && !o.Advanced),"Recommended must not include incompatible or advanced options.");
   var recommendedXml=XDocument.Parse(AnswerFile.Generate(recommended,"Sample-password-42"));
   var removalIds = new[] { "remove-solitaire", "remove-news", "remove-weather", "remove-gethelp", "remove-feedback", "remove-todos" };
   Check(recommended.Options.Where(id => id.StartsWith("remove-")).ToHashSet().SetEquals(removalIds), "Recommended must remove exactly the six selected apps.");
   var removalScript = ReadPayload(recommendedXml, "Script");
   foreach (var id in removalIds)
    Check(removalScript.Contains(Catalog.All.Single(o => o.Id == id).Code), "Recommended XML must include removal code for " + id);
   Check(!removalScript.Contains("Microsoft.WindowsCalculator"), "Optional removals must not leak into Recommended.");
   Check(recommended.DesktopReady && recommended.AutoLogon && recommended.Administrator && recommended.Username.Length>0,"Recommended must create a local administrator and sign in once.");
   foreach(var element in new[]{"AutoLogon","LocalAccount","HideOnlineAccountScreens","HideWirelessSetupInOOBE","HideEULAPage","HideOEMRegistrationScreen","ProtectYourPC","TimeZone"})
    Check(recommendedXml.Descendants().Any(e=>e.Name.LocalName==element),"Recommended XML missing "+element);
   var localePasses=recommendedXml.Descendants().Where(e=>e.Name.LocalName=="settings").ToArray();
   foreach(var pass in new[]{"windowsPE","oobeSystem"})
    foreach(var field in new[]{"UILanguage","InputLocale","SystemLocale","UserLocale"})
     Check(localePasses.Single(e=>(string?)e.Attribute("pass")==pass).Descendants().Any(e=>e.Name.LocalName==field),"Missing locale "+field+" in "+pass);
   Check(!recommendedXml.Descendants().Any(e=>e.Name.LocalName is "SkipMachineOOBE" or "SkipUserOOBE" or "DiskConfiguration" or "InstallTo"),"Do not use deprecated OOBE skips or erase target disks.");
   var firstLogon=recommendedXml.Descendants().Single(e=>e.Name.LocalName=="CommandLine").Value;
   Check(firstLogon.Contains("Extensions.Cleanup") && firstLogon.Length<=1024,"First logon must load embedded cleanup through a short command.");
   var cleanup=ReadPayload(recommendedXml,"Cleanup");
   Check(cleanup.Contains("AutoLogonCount -Value 0") && cleanup.Contains("AutoAdminLogon -Value '0'"),"First logon must disable subsequent automatic logons.");
   Reject(()=>AnswerFile.Generate(recommended,""),"Recommended must require its local password before writing.");
   var incomplete=recommended.Clone();incomplete.Keyboard="Default";
   Reject(()=>AnswerFile.Generate(incomplete,"Sample-password-42"),"Desktop-ready setup must reject missing locale choices.");
   var restored=JsonSerializer.Deserialize<BuildProfile>(JsonSerializer.Serialize(recommended))!;
   InstallationPresets.NormalizeLabel(restored);
   Check(restored.InstallationProfile==InstallationPresets.Recommended,"Profile type must round-trip.");
   restored.Options.Add("hidden");InstallationPresets.NormalizeLabel(restored);
   Check(restored.InstallationProfile==InstallationPresets.Custom,"Edited presets must load as Custom.");
  }
  Check(JsonSerializer.Deserialize<BuildProfile>("{\"Windows\":\"10\",\"Options\":[\"hidden\"]}")!.InstallationProfile==InstallationPresets.Custom,"Legacy profiles must load as Custom.");
  Reject(()=>new BuildProfile {InstallationProfile="Unrecognized"}.Validate(),"Unknown preset accepted.");
  new MainWindow().VerifyInterface(Check);
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test.txt"),$"PASS: {passed} checks. No disks were written and no generated configuration scripts were executed.");
 }
}
