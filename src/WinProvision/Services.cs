using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace WinProvision;
public static class Services
{
 public static readonly string PowerShell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
 public static async Task<string> RunPowerShell(string command)
 {
  var start = new ProcessStartInfo(PowerShell) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
  foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding=[Text.UTF8Encoding]::new(); $ErrorActionPreference='Stop'; " + command)) }) start.ArgumentList.Add(arg);
  using var p = Process.Start(start)!;
  var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
  await p.WaitForExitAsync();
  if (p.ExitCode != 0) throw new IOException((await error).Trim());
  return (await output).Trim();
 }
 public static async Task<List<UsbDisk>> Disks()
 {
  var json = await RunPowerShell("$items=@(Get-Disk | Where-Object { $_.BusType -eq 'USB' -and -not $_.IsBoot -and -not $_.IsSystem -and $_.Size -ge 8GB -and -not $_.IsReadOnly -and -not $_.IsOffline } | ForEach-Object { $d=$_; $v=@(Get-Partition -DiskNumber $d.Number -ErrorAction SilentlyContinue | Where-Object DriveLetter | ForEach-Object { $_.DriveLetter+':' }); [pscustomobject]@{Number=$d.Number;FriendlyName=$d.FriendlyName;UniqueId=$d.UniqueId;Size=$d.Size;Volumes=($v -join ', ')} }); ConvertTo-Json -InputObject $items -Compress");
  return JsonSerializer.Deserialize<List<UsbDisk>>(json) ?? [];
 }
 public static string NewPrivateJob()
 {
  var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinProvision", "Jobs", Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(dir);
  var acl = new DirectorySecurity();
  acl.SetAccessRuleProtection(true, false);
  foreach (var sid in new[] { WindowsIdentity.GetCurrent().User!, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
   acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
  new DirectoryInfo(dir).SetAccessControl(acl);
  return dir;
 }
 public static async Task Build(string iso, UsbDisk disk, BuildProfile profile, string password, string dir, IProgress<string> progress)
 {
  var payload = Path.Combine(dir, "autounattend.xml");
  await File.WriteAllTextAsync(payload, AnswerFile.Generate(profile, password), new UTF8Encoding(true));
  var job = new { IsoPath=Path.GetFullPath(iso), DiskNumber=disk.Number, disk.UniqueId, disk.Size, profile.Windows, profile.Edition, profile.Language, profile.InterfaceLanguage, AnswerFile=payload, AppDirectory=AppContext.BaseDirectory };
  var jobFile=Path.Combine(dir,"job.json");
  await File.WriteAllTextAsync(jobFile,JsonSerializer.Serialize(job,BuildProfile.Json));
  try
  {
   var script=Path.Combine(AppContext.BaseDirectory,"Scripts","Build-Usb.ps1");
   var start=new ProcessStartInfo(PowerShell) { UseShellExecute=true, Verb="runas", WindowStyle=ProcessWindowStyle.Hidden, Arguments=$"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{script}\" -JobPath \"{jobFile}\"" };
   using var process=Process.Start(start) ?? throw new IOException(L.T("Could not start USB writer."));
   string last="";
   while (!process.HasExited)
   {
    var file=Path.Combine(dir,"status.txt");
    try { if(File.Exists(file)) { var value=await File.ReadAllTextAsync(file); if(value!=last) { last=value; progress.Report(value); } } } catch(IOException) { }
    await Task.Delay(600);
   }
   var result=Path.Combine(dir,"result.json");
   if(!File.Exists(result)) throw new IOException(L.T("The writer stopped without a result. The USB may be incomplete. See the build log."));
   using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(result));
   if(!doc.RootElement.GetProperty("Success").GetBoolean()) throw new IOException(doc.RootElement.GetProperty("Message").GetString());
   progress.Report(L.T("USB ready. Safely eject it before unplugging."));
  }
  finally { if(File.Exists(payload)) File.Delete(payload); }
 }
}
