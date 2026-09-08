namespace WinProvision;

public static class InstallationPresets
{
 public const string Default = "Default";
 public const string Recommended = "Recommended";
 public const string Custom = "Custom";

 // Small, reviewable baseline. Personal appearance and hardware choices stay custom.
 public static readonly IReadOnlyList<string> RecommendedOptions = Array.AsReadOnly(new[]
 {
  "extensions", "this-pc", "no-ad-id", "no-tailored", "no-edge-boost", "no-edge-background",
  "hide-online", "hide-wifi", "privacy-oobe"
 });

 public static BuildProfile Create(string kind, BuildProfile source)
 {
  if (kind is not (Default or Recommended)) throw new ArgumentException("Choose a built-in installation profile.");
  return new BuildProfile
  {
   InstallationProfile = kind,
   Windows = source.Windows,
   Edition = source.Edition,
   AppSelectionMode = source.AppSelectionMode,
   SelectedApps = new HashSet<string>(source.SelectedApps),
   DesktopReady = kind == Recommended,
   Username = kind == Recommended ? (string.IsNullOrWhiteSpace(source.Username) ? "User" : source.Username) : "",
   Administrator = true,
   AutoLogon = kind == Recommended,
   Language = kind == Recommended ? (source.Language == "ISO default" ? SuggestedLanguage() : source.Language) : "ISO default",
   Keyboard = kind == Recommended ? (source.Keyboard == "Default" ? SuggestedKeyboard(source.Language == "ISO default" ? SuggestedLanguage() : source.Language) : source.Keyboard) : "Default",
   TimeZone = kind == Recommended ? (source.TimeZone == "Default" ? TimeZoneInfo.Local.Id : source.TimeZone) : "Default",
   ComputerName = kind == Recommended ? source.ComputerName : "",
   Options = kind == Recommended
    ? RecommendedOptions.Where(id => Catalog.All.Single(o => o.Id == id).Supports(source.Windows)).ToHashSet()
    : []
  };
 }
 static string SuggestedLanguage() => System.Globalization.CultureInfo.CurrentCulture.Name switch { "pt-BR" => "pt-BR", "es-ES" => "es-ES", _ => "en-US" };
 static string SuggestedKeyboard(string language) => language switch { "pt-BR" => "Brazil ABNT2", "es-ES" => "Spanish", _ => "US" };

 // Old profile files have no mode and load as Custom. Edited built-in files must
 // also be labelled Custom so the card never hides unexpected custom settings.
 public static void NormalizeLabel(BuildProfile profile)
 {
  if (profile.InstallationProfile == Custom) return;
  var expected = Create(profile.InstallationProfile, profile);
  if (!profile.Options.SetEquals(expected.Options) || profile.Language != expected.Language ||
      profile.Keyboard != expected.Keyboard || profile.TimeZone != expected.TimeZone ||
      profile.ComputerName != expected.ComputerName || profile.Username != expected.Username ||
      profile.Administrator != expected.Administrator || profile.AutoLogon != expected.AutoLogon || profile.DesktopReady != expected.DesktopReady)
   profile.InstallationProfile = Custom;
 }
}
