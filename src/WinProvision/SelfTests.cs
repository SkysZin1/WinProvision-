using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace WinProvision;
public static class SelfTests
{
 public static void Run()
 {
  int passed=0;
  void Check(bool result,string message){if(!result)throw new Exception(message);passed++;}
  void Reject(Action action,string message){try{action();}catch(ArgumentException){passed++;return;}catch(InvalidDataException){passed++;return;}throw new Exception(message);}
  var defaultXml=AnswerFile.Generate(new BuildProfile(),"");
  Check(AppCatalog.Names.Count==30,"Expected the full 30-app catalog.");
  new BuildProfile{SelectedApps=AppCatalog.Names.Keys.ToHashSet(),AppSelectionMode="BeforeBoot"}.Validate();
  var appsProfile=new BuildProfile{AppSelectionMode="BeforeBoot",SelectedApps=["7zip.7zip","Mozilla.Firefox"]};
  var appsRoundTrip=JsonSerializer.Deserialize<BuildProfile>(JsonSerializer.Serialize(appsProfile))!;
  Check(appsRoundTrip.AppSelectionMode=="BeforeBoot" && appsRoundTrip.SelectedApps.SetEquals(appsProfile.SelectedApps),"Application selection must round-trip.");
  var plannedXml=XDocument.Parse(AnswerFile.Generate(appsProfile,""));
  var plannedScript=plannedXml.Descendants().Single(e=>e.Name.LocalName=="Provisioner").Value;
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
  Check(defaultDoc.Descendants().Single(e=>e.Name.LocalName=="Provisioner").Value.Contains("if($Worker)"),"Embedded app picker must contain its worker.");
  foreach(var path in defaultDoc.Descendants().Where(e=>e.Name.LocalName=="Path" && e.Value.Contains("-EncodedCommand")))
  {
   var extraction=System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(path.Value.Split(' ').Last()));
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
   Check(doc.Descendants().Any(e=>e.Name.LocalName=="Value" && e.Value==secret),"Passwords must round-trip through XML escaping.");
   Check(!AnswerFile.Generate(profile,secret,true).Contains("Test&lt;"),"Preview must omit password.");
   Check(!JsonSerializer.Serialize(profile).Contains(secret),"Saved profiles must not include password.");
   Check(!doc.Descendants().Any(e=>e.Name.LocalName is "DiskConfiguration" or "InstallTo"),"No target-disk automation may be generated.");
   var script=doc.Descendants().Single(e=>e.Name.LocalName=="Script").Value;
   Check(script.Contains("WinProvisionDefault") && script.Contains("finally"),"Default-user hive must be unloaded.");
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
   Check(Catalog.All.Where(o=>recommended.Options.Contains(o.Id)).All(o=>o.Supports(os) && !o.Advanced && !o.Id.StartsWith("remove-")),"Recommended must not include incompatible, advanced or removal options.");
   var recommendedXml=XDocument.Parse(AnswerFile.Generate(recommended,"Sample-password-42"));
   Check(recommended.DesktopReady && recommended.AutoLogon && recommended.Administrator && recommended.Username.Length>0,"Recommended must create a local administrator and sign in once.");
   foreach(var element in new[]{"AutoLogon","LocalAccount","HideOnlineAccountScreens","HideWirelessSetupInOOBE","HideEULAPage","HideOEMRegistrationScreen","ProtectYourPC","TimeZone"})
    Check(recommendedXml.Descendants().Any(e=>e.Name.LocalName==element),"Recommended XML missing "+element);
   var localePasses=recommendedXml.Descendants().Where(e=>e.Name.LocalName=="settings").ToArray();
   foreach(var pass in new[]{"windowsPE","oobeSystem"})
    foreach(var field in new[]{"UILanguage","InputLocale","SystemLocale","UserLocale"})
     Check(localePasses.Single(e=>(string?)e.Attribute("pass")==pass).Descendants().Any(e=>e.Name.LocalName==field),"Missing locale "+field+" in "+pass);
   Check(!recommendedXml.Descendants().Any(e=>e.Name.LocalName is "SkipMachineOOBE" or "SkipUserOOBE" or "DiskConfiguration" or "InstallTo"),"Do not use deprecated OOBE skips or erase target disks.");
   var firstLogon=recommendedXml.Descendants().Single(e=>e.Name.LocalName=="CommandLine").Value;
   var cleanup=System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(firstLogon.Split(' ').Last()));
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
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test.txt"),$"PASS: {passed} checks. No disks were written and no generated configuration scripts were executed.");
 }
}
