using System.IO;
using System.Text.Json;

namespace WinProvision;
public sealed class BuildProfile
{
 public int SchemaVersion { get; set; } = 1;
 public string InterfaceLanguage { get; set; } = L.Language;
 public string InstallationProfile { get; set; } = InstallationPresets.Custom;
 public string Windows { get; set; } = "11";
 public string Edition { get; set; } = "Ask during setup";
 public string Language { get; set; } = "ISO default";
 public string Keyboard { get; set; } = "Default";
 public string TimeZone { get; set; } = "Default";
 public string ComputerName { get; set; } = "";
 public string Username { get; set; } = "";
 public bool Administrator { get; set; } = true;
 public bool AutoLogon { get; set; }
 public bool DesktopReady { get; set; }
 public string AppSelectionMode { get; set; } = "AfterBoot";
 public HashSet<string> SelectedApps { get; set; } = [];
 public HashSet<string> Options { get; set; } = [];
 public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
 public BuildProfile Clone() => JsonSerializer.Deserialize<BuildProfile>(JsonSerializer.Serialize(this, Json))!;
 public void Validate()
 {
  if(InterfaceLanguage is not ("pt-BR" or "en-US"))throw new InvalidDataException(L.T("Invalid interface language."));
  if(AppSelectionMode is not ("AfterBoot" or "BeforeBoot") || SelectedApps == null || SelectedApps.Any(id=>!AppCatalog.Names.ContainsKey(id))) throw new InvalidDataException(L.T("Invalid application selection."));
  if(AppSelectionMode == "BeforeBoot" && SelectedApps.Count == 0) throw new InvalidDataException(L.T("Choose at least one app or choose apps after first boot."));
  if (SchemaVersion != 1 || Windows is not ("10" or "11")) throw new InvalidDataException(L.T("Unsupported profile version or Windows target."));
  if (InstallationProfile is not (InstallationPresets.Default or InstallationPresets.Recommended or InstallationPresets.Custom)) throw new InvalidDataException(L.T("Unknown installation profile."));
  if (!new[] { "Ask during setup", "Home", "Pro" }.Contains(Edition)) throw new InvalidDataException(L.T("Unsupported Windows edition."));
  if (!new[] { "ISO default", "en-US", "pt-BR", "es-ES" }.Contains(Language)) throw new InvalidDataException(L.T("Unsupported language."));
  if (!new[] { "Default", "US", "Brazil ABNT2", "Spanish" }.Contains(Keyboard)) throw new InvalidDataException(L.T("Unsupported keyboard."));
  if (TimeZone != "Default") TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
  if (ComputerName.Length > 15 || (ComputerName.Length > 0 && (!System.Text.RegularExpressions.Regex.IsMatch(ComputerName, @"^[A-Za-z0-9][A-Za-z0-9-]*$" ) || ComputerName.All(char.IsDigit)))) throw new InvalidDataException(L.T("Computer name: 1–15 letters, digits or hyphens; cannot contain only digits."));
  if (Username.Length > 20 || Username.IndexOfAny("\"/\\[]:;|=,+*?<>@".ToCharArray()) >= 0 || Username.EndsWith('.') || Username.Any(char.IsControl) || (Username.Length > 0 && string.IsNullOrWhiteSpace(Username))) throw new InvalidDataException(L.T("Enter a valid local account name (up to 20 characters)."));
  if (new[] { "administrator", "guest", "defaultaccount", "wdagutilityaccount" }.Contains(Username.ToLowerInvariant())) throw new InvalidDataException(L.T("Choose a name other than a reserved Windows account."));
  if (AutoLogon && (string.IsNullOrWhiteSpace(Username) || !Administrator)) throw new InvalidDataException(L.T("One-time automatic logon requires a local administrator account."));
  if (Options == null || Options.Any(id => !Catalog.All.Any(o => o.Id == id))) throw new InvalidDataException(L.T("The profile contains an unknown option."));
  var incompatible = Catalog.All.Where(o => Options.Contains(o.Id) && !o.Supports(Windows)).ToArray();
  if (incompatible.Length > 0) throw new InvalidDataException(L.T("Incompatible options: ") + string.Join(", ", incompatible.Select(o => o.DisplayTitle)));
 }
}
public sealed record Tweak(string Id, string Category, string Title, string Description, string Compatibility, string Scope, string Code, bool Advanced = false)
{
 public string DisplayTitle => L.T(Title);
 public string DisplayDescription => L.T(Description);
 public string DisplayCategory => L.T(Category);
 public string DisplayCompatibility => L.T(Compatibility);
 public bool Supports(string os) => Compatibility.StartsWith("Windows 10 & 11") || Compatibility.StartsWith("Windows " + os + " only");
}
public sealed class UsbDisk
{
 public int Number { get; set; }
 public string FriendlyName { get; set; } = "";
 public string UniqueId { get; set; } = "";
 public ulong Size { get; set; }
 public string Volumes { get; set; } = "";
 public override string ToString() => L.F($"Disk {Number}  ·  {FriendlyName}  ·  {Size / 1e9:0.0} GB  ·  {Volumes}");
}
