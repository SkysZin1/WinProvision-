using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace WinProvision;

/// <summary>Shared UI translations. Profile keys, package IDs and worker states stay language-neutral.</summary>
public static class L
{
 sealed record Bundle(Dictionary<string,string> Translations, Dictionary<string,string> Resources);
 static readonly Bundle bundle = ReadBundle();
 public static string Language { get; private set; } = CultureInfo.CurrentUICulture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en-US";
 public static string Json { get; } = ReadJson();
 static string PreferencePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WinProvision","language.json");
 public static void LoadPreference()
 {
  try { if(File.Exists(PreferencePath)){var language=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(PreferencePath))?.GetValueOrDefault("Language");if(language is "pt-BR" or "en-US")SetLanguage(language);} }
  catch(IOException) { }
  catch(UnauthorizedAccessException) { }
  catch(JsonException) { }
 }
 public static void SavePreference()
 {
  try { Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);File.WriteAllText(PreferencePath,JsonSerializer.Serialize(new{Language})); }
  catch(IOException) { }
  catch(UnauthorizedAccessException) { }
 }
 static string ReadJson()
 {
  using var stream=typeof(L).Assembly.GetManifestResourceStream("WinProvision.Assets.Translations.json") ?? throw new InvalidOperationException("Missing translations.");
  using var reader=new StreamReader(stream);
  return reader.ReadToEnd();
 }
 static Bundle ReadBundle() => JsonSerializer.Deserialize<Bundle>(ReadJson())!;
 public static string T(string value) => Language=="pt-BR"
  ? bundle.Translations.GetValueOrDefault(value,value)
  : value;
 public static string F(FormattableString value) => string.Format(CultureInfo.GetCultureInfo(Language),T(value.Format),value.GetArguments());
 public static void SetLanguage(string language)
 {
  if(language is not ("pt-BR" or "en-US"))throw new ArgumentException(T("Invalid interface language."));
  Language=language;
  if(Application.Current is { } app)
   foreach(var entry in bundle.Resources)app.Resources[entry.Key]=T(entry.Value).Replace("{version}", typeof(L).Assembly.GetName().Version!.ToString(3));
 }
 public static LocalizedChoice[] Choices(IEnumerable<string> values) => values.Select(value=>new LocalizedChoice(value,T(value))).ToArray();
 public static LocalizedChoice[] TimeZones(bool includeDefault) =>
  (includeDefault ? new[]{new LocalizedChoice("Default",T("Default"))} : Array.Empty<LocalizedChoice>())
  .Concat(TimeZoneInfo.GetSystemTimeZones().Select(zone=>new LocalizedChoice(zone.Id,TimeZoneLabel(zone.Id)))).ToArray();
 public static string TimeZoneLabel(string id)
 {
  if(id=="Default")return T(id);
  var zone=TimeZoneInfo.FindSystemTimeZoneById(id);
  var offset=zone.BaseUtcOffset;
  var name=TimeZoneInfo.TryConvertWindowsIdToIanaId(id,out var iana)?iana:id;
  return $"UTC{(offset.Ticks<0?"-":"+")}{offset.Duration():hh\\:mm} · {name}";
 }
}
public sealed record LocalizedChoice(string Value,string Label);
