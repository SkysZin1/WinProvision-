using System.IO;
using System.Windows;

namespace WinProvision;
public partial class App : Application
{
 protected override void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  L.SetLanguage(L.Language);
  if (e.Args.Contains("--self-test"))
  {
   try { SelfTests.Run(); Shutdown(0); }
   catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.txt"), ex.ToString()); Shutdown(1); }
   return;
  }
  DispatcherUnhandledException += (_, args) => { MessageBox.Show(args.Exception.Message, "WinProvision", MessageBoxButton.OK, MessageBoxImage.Error); args.Handled = true; };
  L.LoadPreference();
  new MainWindow().Show();
 }
}
