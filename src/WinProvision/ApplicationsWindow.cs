using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace WinProvision;
public sealed class ApplicationsWindow : Window
{
 public BuildProfile Result {get;}
 public ApplicationsWindow(BuildProfile source)
 {
  Result=source.Clone();Title=L.T("Applications");Width=740;Height=540;MinWidth=650;MinHeight=480;WindowStartupLocation=WindowStartupLocation.CenterOwner;
  var panel=new StackPanel{Margin=new Thickness(28)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  panel.Children.Add(new TextBlock{Text=L.T("When should apps be selected?"),FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,22)});
  var later=new RadioButton{Content=L.T("Choose after first boot"),IsChecked=Result.AppSelectionMode=="AfterBoot",Margin=new Thickness(0,0,0,14)};
  var now=new RadioButton{Content=L.T("Choose now — install automatically after first sign-in"),IsChecked=Result.AppSelectionMode=="BeforeBoot",Margin=new Thickness(0,0,0,18)};
  panel.Children.Add(later);panel.Children.Add(now);
  var choose=new Button{Content=L.T("Choose apps…"),HorizontalAlignment=HorizontalAlignment.Left,IsEnabled=now.IsChecked==true};panel.Children.Add(choose);
  var summary=new TextBlock{Text=string.Join(", ",Result.SelectedApps.Select(id=>AppCatalog.Names[id])),Margin=new Thickness(0,14,0,18)};panel.Children.Add(summary);
  panel.Children.Add(new TextBlock{Text=L.T("Choose now authorizes installation of the selected apps and accepts their terms and the WinGet source terms. Downloads start on the installed Windows, never on this PC. Internet is required; permission prompts or failures may need attention. Use Recommended for automatic Windows setup and sign-in."),FontSize=12,Margin=new Thickness(0,0,0,18)});
  var save=new Button{Content=L.T("Save application settings"),HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(save);
  later.Checked+=(_,_)=>{Result.AppSelectionMode="AfterBoot";choose.IsEnabled=false;};
  now.Checked+=(_,_)=>{Result.AppSelectionMode="BeforeBoot";choose.IsEnabled=true;};
  bool choosing=false;
  Closing+=(_,e)=>{if(choosing)e.Cancel=true;};
  choose.Click+=async(_,_)=>{
   choosing=true;choose.IsEnabled=save.IsEnabled=later.IsEnabled=now.IsEnabled=false;
   try{
    var folder=Services.NewPrivateJob();var selection=Path.Combine(folder,"apps.json");
    await File.WriteAllTextAsync(selection,JsonSerializer.Serialize(new{Ids=Result.SelectedApps,Saved=false}));
    var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"System32\WindowsPowerShell\v1.0\powershell.exe")){UseShellExecute=false,CreateNoWindow=true};
    foreach(var arg in new[]{"-NoProfile","-STA","-ExecutionPolicy","Bypass","-File",Path.Combine(AppContext.BaseDirectory,"Scripts","Provisioner.ps1"),"-Configure","-SelectionPath",selection,"-UiLanguage",L.Language})start.ArgumentList.Add(arg);
    using var process=Process.Start(start)??throw new IOException(L.T("Cannot open app selection."));await process.WaitForExitAsync();
    using var response=JsonDocument.Parse(await File.ReadAllTextAsync(selection));
    if(response.RootElement.GetProperty("Saved").GetBoolean()){
     var ids=response.RootElement.GetProperty("Ids").EnumerateArray().Select(x=>x.GetString()!).ToHashSet();
     if(ids.Any(id=>!AppCatalog.Names.ContainsKey(id)))throw new InvalidDataException(L.T("Unknown app selection."));
     Result.SelectedApps=ids;summary.Text=string.Join(", ",ids.Select(id=>AppCatalog.Names[id]));
    }
   }catch(Exception ex){MessageBox.Show(this,ex.Message,L.T("Applications"));}
   finally{choosing=false;choose.IsEnabled=save.IsEnabled=later.IsEnabled=now.IsEnabled=true;}
  };
  save.Click+=(_,_)=>{if(Result.AppSelectionMode=="BeforeBoot"&&Result.SelectedApps.Count==0){MessageBox.Show(this,L.T("Choose at least one app."));return;}DialogResult=true;};
 }
}
