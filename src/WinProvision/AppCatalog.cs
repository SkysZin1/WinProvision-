namespace WinProvision;
public static class AppCatalog
{
 public static readonly IReadOnlyDictionary<string,string> Names = Load();
 static IReadOnlyDictionary<string,string> Load(){
 var names=new Dictionary<string,string> {
  ["Google.Chrome"]="Google Chrome",["Mozilla.Firefox"]="Mozilla Firefox",["Brave.Brave"]="Brave",
  ["7zip.7zip"]="7-Zip",["VideoLAN.VLC"]="VLC",["Spotify.Spotify"]="Spotify",
  ["Discord.Discord"]="Discord",["Valve.Steam"]="Steam",["TheDocumentFoundation.LibreOffice"]="LibreOffice",
  ["Microsoft.VisualStudioCode"]="Visual Studio Code",["Notepad++.Notepad++"]="Notepad++",["Git.Git"]="Git"
 };
 using var stream=typeof(AppCatalog).Assembly.GetManifestResourceStream("WinProvision.Assets.AdditionalApps.json")??throw new InvalidOperationException("Missing app catalog");
 using var document=System.Text.Json.JsonDocument.Parse(stream);
 foreach(var app in document.RootElement.EnumerateArray())names.Add(app.GetProperty("Id").GetString()!,app.GetProperty("Name").GetString()!);
 return names;
 }
}
