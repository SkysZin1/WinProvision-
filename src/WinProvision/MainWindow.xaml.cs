using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinProvision;
public partial class MainWindow : Window
{
 BuildProfile profile = new() { InstallationProfile = InstallationPresets.Default };
 BuildProfile? customDraft;
 string customPassword = "";
 bool editingCustom;
 string password = "";
 string category = "All settings";
 string? iso;
 string? jobDirectory;
 int page;
 bool initialized;
 bool busy;
 static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(11,116,103));
 static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(96,116,128));
 public MainWindow()
 {
  InitializeComponent();
  EditionChoice.DisplayMemberPath="Label";EditionChoice.SelectedValuePath="Value";
  EditionChoice.ItemsSource=L.Choices(["Ask during setup","Home","Pro"]); EditionChoice.SelectedIndex=0;
  InterfaceLanguageChoice.SelectedIndex=L.Language=="pt-BR"?0:1;
  System.Windows.Automation.AutomationProperties.SetName(Search,L.T("Search settings"));
  Search.ToolTip=L.T("Search settings, for example: Explorer, privacy, Windows 11");
  initialized=true; RenderCategories(); RenderOptions(); ShowPage(0);
  Loaded += async (_,_) => await RefreshDisks();
  Closing += (_,e) => { if(busy) { e.Cancel=true; MessageBox.Show(this,L.T("Wait until USB creation finishes. Interrupting a write can leave the media incomplete."),L.T("USB creation in progress")); } };
 }
 BuildProfile Current()
 {
  profile.Windows=WindowsChoice.SelectedIndex==1?"10":"11";
  profile.Edition=EditionChoice.SelectedValue as string ?? "Ask during setup";
  profile.InterfaceLanguage=L.Language;
  return profile;
 }
 void Applications_Click(object sender, RoutedEventArgs e)
 {
  if(busy)return;
  var dialog=new ApplicationsWindow(Current()){Owner=this};
  if(dialog.ShowDialog()==true){profile.AppSelectionMode=dialog.Result.AppSelectionMode;profile.SelectedApps=dialog.Result.SelectedApps;if(customDraft!=null){customDraft.AppSelectionMode=profile.AppSelectionMode;customDraft.SelectedApps=new(profile.SelectedApps);}if(page==2)UpdateReview();StatusText.Text=L.T("Application settings saved for this installation.");}
 }
 void InterfaceLanguage_Changed(object sender,SelectionChangedEventArgs e)
 {
  if(!initialized || InterfaceLanguageChoice.SelectedItem is not ComboBoxItem item)return;
  Current();L.SetLanguage((string)item.Tag);profile.InterfaceLanguage=L.Language;
  if(IsVisible)L.SavePreference();
  if(customDraft!=null)customDraft.InterfaceLanguage=L.Language;
  EditionChoice.ItemsSource=L.Choices(["Ask during setup","Home","Pro"]);EditionChoice.SelectedValue=profile.Edition;
  bool custom=editingCustom;ShowPage(page);if(custom && page==1)OpenCustomEditor();
  RenderCategories();RenderOptions();UpdateCount();
  DiskChoice.Items.Refresh();
  if(DiskChoice.ItemsSource is List<UsbDisk> disks)DiskStatus.Text=disks.Count==0?L.T("No eligible USB disks found. Connect an 8 GiB or larger writable USB and refresh."):L.F($"{disks.Count} USB device(s) available. Select one explicitly; all its partitions belong to that device.");
  System.Windows.Automation.AutomationProperties.SetName(Search,L.T("Search settings"));
  Search.ToolTip=L.T("Search settings, for example: Explorer, privacy, Windows 11");
 }
 void ShowPage(int next)
 {
  if(busy) return;
  page=next;
  if(next==1) editingCustom=false;
  MediaPage.Visibility=next==0?Visibility.Visible:Visibility.Collapsed;
  ProfilePage.Visibility=next==1?Visibility.Visible:Visibility.Collapsed;
  OptionsPage.Visibility=Visibility.Collapsed;
  ReviewPage.Visibility=next==2?Visibility.Visible:Visibility.Collapsed;
  PageEyebrow.Text=L.F($"STEP 0{next+1} / 03");
  PageTitle.Text=next switch {0=>L.T("A fresh start begins here."),1=>L.T("Choose your installation style."),_=>L.T("One last look. Then a fresh start.")};
  PageSubtitle.Text=next switch {0=>L.T("Bring your Windows ISO. Build an installation that feels like yours."),1=>L.T("Start with Windows defaults, our suggested setup, or your own configuration."),_=>L.T("Review the device and settings before anything is erased.")};
  NextButton.Content=next==0?L.T("Windows settings →"):L.T("Review installation →");
  NextButton.Visibility=next==2?Visibility.Collapsed:Visibility.Visible;
  var nav=new[] {MediaNav,OptionsNav,ReviewNav};
  for(int i=0;i<3;i++) { nav[i].Background=i==next?new SolidColorBrush(Color.FromRgb(42,82,84)):Brushes.Transparent; nav[i].Foreground=i==next?Brushes.White:new SolidColorBrush(Color.FromRgb(165,195,192)); nav[i].BorderThickness=new Thickness(0); }
  if(next==2) UpdateReview();
  if(next==1) RenderPresetSummary();
 }
 void Media_Click(object s,RoutedEventArgs e)=>ShowPage(0);
 void Options_Click(object s,RoutedEventArgs e)=>ShowPage(1);
 void Review_Click(object s,RoutedEventArgs e)=>ShowPage(2);
 void Next_Click(object s,RoutedEventArgs e)
 {
  if(page==1 && !editingCustom && profile.InstallationProfile==InstallationPresets.Custom) OpenCustomEditor();
  else ShowPage(Math.Min(2,page+1));
 }
 void DefaultProfile_Click(object s,RoutedEventArgs e)=>ChoosePreset(InstallationPresets.Default);
 void RecommendedProfile_Click(object s,RoutedEventArgs e)=>ChoosePreset(InstallationPresets.Recommended);
 void ChoosePreset(string kind)
 {
  if(busy)return;
  if(profile.InstallationProfile==InstallationPresets.Custom) { customDraft=Current().Clone();customPassword=password; }
  profile=InstallationPresets.Create(kind,Current());password=kind==InstallationPresets.Recommended?InstallationPresets.RecommendedPassword:"";
  EraseConsent.IsChecked=false;ConfirmText.Clear();
  RenderPresetSummary();UpdateCount();
 }
 void CustomProfile_Click(object s,RoutedEventArgs e)
 {
  if(busy)return;
  var source=Current();
  if(source.InstallationProfile!=InstallationPresets.Custom)
  {
   if(customDraft!=null)
   {
    profile=customDraft.Clone();profile.Windows=source.Windows;profile.Edition=source.Edition;profile.AppSelectionMode=source.AppSelectionMode;profile.SelectedApps=new(source.SelectedApps);
    profile.Options.RemoveWhere(id=>!Catalog.All.Single(o=>o.Id==id).Supports(profile.Windows));
    password=customPassword;
   }
   else profile=source.Clone();
   profile.InstallationProfile=InstallationPresets.Custom;
  }
  OpenCustomEditor();
 }
 void CustomizePreset_Click(object s,RoutedEventArgs e)
 {
  if(busy)return;
  // This action explicitly starts a new custom draft from the selected preset.
  profile=Current().Clone();profile.InstallationProfile=InstallationPresets.Custom;
  OpenCustomEditor();
 }
 void BackToProfiles_Click(object s,RoutedEventArgs e)=>ShowPage(1);
 void OpenCustomEditor()
 {
  editingCustom=true;ProfilePage.Visibility=Visibility.Collapsed;OptionsPage.Visibility=Visibility.Visible;
  PageTitle.Text=L.T("Make Windows feel like yours.");
  PageSubtitle.Text=L.T("Custom installation · Unchecked options keep Windows defaults.");
  NextButton.Content=L.T("Review installation →");
  EraseConsent.IsChecked=false;ConfirmText.Clear();
  RenderOptions();
 }
 void RenderPresetSummary()
 {
  var kind=profile.InstallationProfile;
  var cards=new[]{DefaultProfileButton,RecommendedProfileButton,CustomProfileButton};
  var names=new[]{InstallationPresets.Default,InstallationPresets.Recommended,InstallationPresets.Custom};
  for(int i=0;i<cards.Length;i++)
  {
   bool selected=kind==names[i];
   cards[i].Background=selected?new SolidColorBrush(Color.FromRgb(226,240,233)):Brushes.White;
   cards[i].BorderBrush=selected?Accent:new SolidColorBrush(Color.FromRgb(213,223,220));
   cards[i].BorderThickness=new Thickness(selected?2:1);
  }
  DefaultProfileState.Text=kind==InstallationPresets.Default?L.T("SELECTED"):L.T("WINDOWS DEFAULTS");
  RecommendedProfileState.Text=kind==InstallationPresets.Recommended?L.T("SELECTED"):L.T("DESKTOP READY");
  CustomProfileState.Text=kind==InstallationPresets.Custom?L.T("SELECTED"):L.T("FULL CONTROL");
  PresetSummaryTitle.Text=L.T(kind)+" · Windows "+profile.Windows;
  PresetSummaryIntro.Text=kind switch
  {
   InstallationPresets.Default=>L.T("No optional tweaks. Windows Setup handles account creation, language, region and privacy choices."),
   InstallationPresets.Recommended=>L.T("Fill in the account and regional settings now. Setup will create this local administrator, supply the OOBE answers and sign in automatically once to reach the desktop."),
   _=>L.F($"Your configuration contains {profile.Options.Count} optional setting(s). Open Custom to review account, region and advanced preferences.")
  };
  PresetSummaryOptions.Children.Clear();
  if(kind==InstallationPresets.Recommended) RenderIdentity(PresetSummaryOptions,true);
  foreach(var option in Catalog.All.Where(o=>profile.Options.Contains(o.Id)))
  {
   var item=new StackPanel {Margin=new Thickness(0,0,0,10)};
   item.Children.Add(Text("• "+option.DisplayTitle,14));
   var detail=Text(option.DisplayCompatibility,11,Muted);detail.Margin=new Thickness(13,3,0,0);item.Children.Add(detail);
   PresetSummaryOptions.Children.Add(item);
  }
  PresetSummaryNote.Text=kind switch
  {
   InstallationPresets.Recommended=>L.T("The Windows installation disk is still selected manually. Setup license screens are accepted automatically; activation is separate. Automatic sign-in is disabled after the first logon. Keeps security features. Removes Solitaire Collection, Microsoft News, Weather, Get Help, Feedback Hub and Microsoft To Do when present. New Windows builds may still introduce prompts; validate your ISO in a VM before deployment."),
   InstallationPresets.Default=>L.T("The Windows version and edition from step 1 are retained. Existing custom settings are kept as a draft for this session."),
   _=>L.T("Your custom draft is kept while you try another profile during this session. Saving a profile saves the selected configuration only.")
  };
  CustomizePresetButton.Content=kind==InstallationPresets.Custom?L.T("Continue customizing →"):L.T("Customize this profile →");
  NextButton.Content=kind==InstallationPresets.Custom?L.T("Customize settings →"):L.T("Review installation →");
 }
 void Browse_Click(object s,RoutedEventArgs e)
 {
  var dialog=new OpenFileDialog {Title=L.T("Select an official Windows ISO"),Filter="Windows ISO (*.iso)|*.iso",CheckFileExists=true};
  if(dialog.ShowDialog(this)==true) { iso=dialog.FileName; IsoPath.Text=iso; StatusText.Text=L.T("ISO selected. Contents will be validated before the USB is erased."); }
 }
 async void Refresh_Click(object s,RoutedEventArgs e)=>await RefreshDisks();
 async Task RefreshDisks()
 {
  RefreshButton.IsEnabled=false; DiskStatus.Text=L.T("Reading USB devices…"); DiskChoice.ItemsSource=null;
  try { var disks=await Services.Disks(); DiskChoice.ItemsSource=disks; DiskChoice.SelectedIndex=-1; DiskStatus.Text=disks.Count==0?L.T("No eligible USB disks found. Connect an 8 GiB or larger writable USB and refresh."):L.F($"{disks.Count} USB device(s) available. Select one explicitly; all its partitions belong to that device."); }
  catch(Exception ex) { DiskStatus.Text=L.T("Could not list USB devices. ") + ex.Message; }
  finally { RefreshButton.IsEnabled=true; }
 }
 void Windows_Changed(object s,SelectionChangedEventArgs e)
 {
  if(!initialized) return;
  Current(); int removed=profile.Options.RemoveWhere(id=>!Catalog.All.First(o=>o.Id==id).Supports(profile.Windows));
  RenderOptions(); EraseConsent.IsChecked=false; ConfirmText.Clear();
  StatusText.Text=removed>0?L.F($"Removed {removed} setting(s) incompatible with Windows {profile.Windows}."):L.F($"Target: Windows {profile.Windows}. Incompatible settings are disabled.");
 }
 void Download_Click(object s,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo("https://www.microsoft.com/software-download/"+(WindowsChoice.SelectedIndex==1?"windows10":"windows11")){UseShellExecute=true});
 void RenderCategories()
 {
  Categories.Children.Clear();
  foreach(var name in new[] {"All settings","Identity & region"}.Concat(Catalog.All.Select(o=>o.Category).Distinct()))
  {
   var b=new Button {Content=L.T(name),Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,0,6,6),FontSize=12,Background=name==category?Accent:Brushes.White,Foreground=name==category?Brushes.White:Muted};
   b.Click+=(_,_)=> {category=name;RenderCategories();RenderOptions();OptionsScroll.ScrollToTop();};
   Categories.Children.Add(b);
  }
 }
 void Search_Changed(object s,TextChangedEventArgs e) { if(initialized) RenderOptions(); }
 static TextBlock Text(string value,double size=14,Brush? color=null)=>new() {Text=value,FontSize=size,Foreground=color??new SolidColorBrush(Color.FromRgb(23,42,52)),TextWrapping=TextWrapping.Wrap};
 static Border Card(UIElement content)=>new() {Child=content,Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(222,231,226)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(19),Margin=new Thickness(0,0,7,10)};
 void RenderOptions()
 {
  if(!initialized) return;
  OptionCards.Children.Clear();
  var query=Search.Text.Trim();
  if((category is "All settings" or "Identity & region") && query.Length==0) RenderIdentity();
  var shown=Catalog.All.Where(o=>(category=="All settings" || category==o.Category) && (query.Length==0 || (o.DisplayTitle+" "+o.DisplayDescription+" "+o.DisplayCategory+" "+o.DisplayCompatibility).Contains(query,StringComparison.OrdinalIgnoreCase))).ToArray();
  foreach(var option in shown)
  {
   bool supported=option.Supports(profile.Windows);
   var grid=new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
   var stack=new StackPanel {Margin=new Thickness(0,0,18,0)};
   var title=Text(option.DisplayTitle,15);title.FontWeight=FontWeights.SemiBold;stack.Children.Add(title);
   var description=Text(option.DisplayDescription,12,Muted); description.Margin=new Thickness(0,6,0,7);stack.Children.Add(description);
   var badge=Text(option.DisplayCompatibility+(option.Advanced?L.T("  ·  Advanced change"):"")+(!supported?L.T("  ·  Unavailable for selected Windows"):""),11,supported?Accent:Brushes.DarkGoldenrod);stack.Children.Add(badge);
   var toggle=new CheckBox {IsChecked=profile.Options.Contains(option.Id),IsEnabled=supported,VerticalAlignment=VerticalAlignment.Center,ToolTip=option.DisplayTitle};
   System.Windows.Automation.AutomationProperties.SetName(toggle,option.DisplayTitle);
   toggle.Checked+=(_,_)=> {profile.Options.Add(option.Id);UpdateCount();};toggle.Unchecked+=(_,_)=> {profile.Options.Remove(option.Id);UpdateCount();};
   Grid.SetColumn(toggle,1);grid.Children.Add(stack);grid.Children.Add(toggle);OptionCards.Children.Add(Card(grid));
  }
  if(shown.Length==0 && !(query.Length==0 && category=="Identity & region")) OptionCards.Children.Add(Text(L.T("No settings match your search."),15,Muted));
 }
 void UpdateCount()=>StatusText.Text=L.F($"{profile.Options.Count} of {Catalog.All.Count} optional settings selected · Windows {profile.Windows}.");
 void RenderIdentity(StackPanel? destination=null,bool recommended=false)
 {
  var stack=new StackPanel(); var title=Text(recommended?L.T("Set up before installation"):L.T("Identity & region"),18);title.FontWeight=FontWeights.SemiBold;stack.Children.Add(title);
  var note=Text(recommended?L.T("Windows 10 & 11 · Confirm language, keyboard and time zone for the target PC. The language must match the ISO. Set a local account password before creating the USB."):L.T("Windows 10 & 11 · Blank names and default selections leave these choices to Windows Setup. Display language must exist in the ISO."),12,Muted);note.Margin=new Thickness(0,7,0,15);stack.Children.Add(note);
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());
  void AddField(string label,Control input,int row,int column)
  {
   while(grid.RowDefinitions.Count<=row)grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
   var container=new StackPanel {Margin=new Thickness(column==0?0:10,0,column==0?10:0,14)};
   var text=Text(label,12,Muted);text.Margin=new Thickness(0,0,0,5);container.Children.Add(text);container.Children.Add(input);
   System.Windows.Automation.AutomationProperties.SetName(input,label);
   Grid.SetRow(container,row);Grid.SetColumn(container,column);grid.Children.Add(container);
  }
  ComboBox Choice(string[] values,string current,Action<string> set)
  {var control=new ComboBox {ItemsSource=L.Choices(values),DisplayMemberPath="Label",SelectedValuePath="Value",SelectedValue=current};control.SelectionChanged+=(_,_)=> { if(control.SelectedValue is string v)set(v); };return control;}
  AddField(L.T("Display language"),Choice(recommended?["en-US","pt-BR","es-ES"]:["ISO default","en-US","pt-BR","es-ES"],profile.Language,v=>profile.Language=v),0,0);
  AddField(L.T("Keyboard"),Choice(recommended?["US","Brazil ABNT2","Spanish"]:["Default","US","Brazil ABNT2","Spanish"],profile.Keyboard,v=>profile.Keyboard=v),0,1);
  var zones=new ComboBox {ItemsSource=L.TimeZones(!recommended),DisplayMemberPath="Label",SelectedValuePath="Value",SelectedValue=profile.TimeZone};
  zones.SelectionChanged+=(_,_)=>{if(zones.SelectedValue is string v)profile.TimeZone=v;};
  AddField(L.T("Time zone"),zones,1,0);
  var name=new TextBox {Text=profile.ComputerName,MaxLength=15};name.TextChanged+=(_,_)=>profile.ComputerName=name.Text;AddField(L.T("Computer name (optional)"),name,1,1);
  var user=new TextBox {Text=profile.Username,MaxLength=20};user.TextChanged+=(_,_)=>profile.Username=user.Text;AddField(recommended?L.T("Local account name (required)"):L.T("Local account name (optional)"),user,2,0);
  var pass=new PasswordBox {Password=password,Padding=new Thickness(10,8,10,8)};pass.PasswordChanged+=(_,_)=>password=pass.Password;AddField(L.T("Local account password (never saved in profiles)"),pass,2,1);
  if(recommended) stack.Children.Add(Text(L.T("Recommended default password: 123. You can change it here."),12,Muted));
  stack.Children.Add(grid);
  var admin=new CheckBox {Content=L.T("Make the local account an administrator"),IsChecked=profile.Administrator,IsEnabled=!recommended,Margin=new Thickness(0,0,0,12)};
  admin.Checked+=(_,_)=>profile.Administrator=true;admin.Unchecked+=(_,_)=>profile.Administrator=false;stack.Children.Add(admin);
  var auto=new CheckBox {Content=L.T("Automatically sign in once (requires local administrator and password)"),IsChecked=profile.AutoLogon,IsEnabled=!recommended};
  auto.Checked+=(_,_)=>profile.AutoLogon=true;auto.Unchecked+=(_,_)=>profile.AutoLogon=false;stack.Children.Add(auto);
  if(!recommended) {
   var ready=new CheckBox {Content=L.T("Automate OOBE to reach the desktop (requires account, region and OOBE options)"),IsChecked=profile.DesktopReady,Margin=new Thickness(0,12,0,0)};
   ready.Checked+=(_,_)=>profile.DesktopReady=true;ready.Unchecked+=(_,_)=>profile.DesktopReady=false;stack.Children.Add(ready);
  }
  var foot=Text(L.T("User preferences are applied to the default profile before account creation. Individual tweaks still need installation testing on the selected Windows build."),11,Muted);foot.Margin=new Thickness(0,14,0,0);stack.Children.Add(foot);
  (destination??OptionCards).Children.Add(Card(stack));
 }
 void Reset_Click(object s,RoutedEventArgs e)
 {
  var current=Current();profile=new BuildProfile {Windows=current.Windows,Edition=current.Edition,AppSelectionMode=current.AppSelectionMode,SelectedApps=new(current.SelectedApps)};password="";RenderOptions();UpdateCount();
 }
 void Save_Click(object s,RoutedEventArgs e)
 {
  if(busy)return;
  try {var p=Current();p.Validate();var d=new SaveFileDialog {Filter=L.T("WinProvision profile (*.json)")+"|*.json",FileName=L.T("my-winprovision-profile.json")};if(d.ShowDialog(this)==true){File.WriteAllText(d.FileName,JsonSerializer.Serialize(p,BuildProfile.Json));StatusText.Text=L.T("Profile saved. Passwords and USB device selection are not included.");}}
  catch(Exception ex){ShowError(ex);}
 }
 void Load_Click(object s,RoutedEventArgs e)
 {
  if(busy)return;
  var d=new OpenFileDialog {Filter=L.T("WinProvision profile (*.json)")+"|*.json"};if(d.ShowDialog(this)!=true)return;
  try {
   if(new FileInfo(d.FileName).Length>1024*1024)throw new InvalidDataException(L.T("Profile file is too large."));
   var loaded=JsonSerializer.Deserialize<BuildProfile>(File.ReadAllText(d.FileName))??throw new InvalidDataException(L.T("Empty profile."));loaded.Validate();InstallationPresets.NormalizeLabel(loaded);
   initialized=false;profile=loaded;password="";L.SetLanguage(profile.InterfaceLanguage);
   InterfaceLanguageChoice.SelectedIndex=L.Language=="pt-BR"?0:1;
   WindowsChoice.SelectedIndex=profile.Windows=="10"?1:0;
   EditionChoice.ItemsSource=L.Choices(["Ask during setup","Home","Pro"]);EditionChoice.SelectedValue=profile.Edition;initialized=true;
   RenderCategories();
   customDraft=null;customPassword="";
   EraseConsent.IsChecked=false;ConfirmText.Clear();ShowPage(page);RenderOptions();
   System.Windows.Automation.AutomationProperties.SetName(Search,L.T("Search settings"));
   Search.ToolTip=L.T("Search settings, for example: Explorer, privacy, Windows 11");
   DiskChoice.Items.Refresh();
   if(DiskChoice.ItemsSource is List<UsbDisk> disks)DiskStatus.Text=disks.Count==0?L.T("No eligible USB disks found. Connect an 8 GiB or larger writable USB and refresh."):L.F($"{disks.Count} USB device(s) available. Select one explicitly; all its partitions belong to that device.");
   StatusText.Text=L.T("Profile loaded. Re-enter any local account password before creating media.");
  }catch(Exception ex){initialized=true;ShowError(ex);}
 }
 void UpdateReview()
 {
  var p=Current();var disk=DiskChoice.SelectedItem as UsbDisk;
  var selected=Catalog.All.Where(o=>p.Options.Contains(o.Id)).Select(o=>"  • "+o.DisplayTitle);
  ReviewSummary.Text=L.F($"Profile: {L.T(p.InstallationProfile)}\nISO: {(iso==null?L.T("Not selected"):Path.GetFileName(iso))}\nUSB: {disk?.ToString()??L.T("Not selected")}\nTarget: Windows {p.Windows} · {L.T(p.Edition)} · x64\nBoot: UEFI / GPT / FAT32 (up to 30 GiB)\nWindows installation disk: choose manually during Setup\nAccount: {(p.Username.Length==0?L.T("Create during Windows Setup"):p.Username+L.T(p.Administrator?" · Administrator":" · Standard user"))}\nLanguage: {L.T(p.Language)} · Keyboard: {L.T(p.Keyboard)}\nComputer name: {(p.ComputerName.Length==0?L.T("Windows default"):p.ComputerName)} · Time zone: {L.TimeZoneLabel(p.TimeZone)}\nOne-time automatic logon: {L.T(p.AutoLogon?"Yes":"No")}\n\n{p.Options.Count} optional setting(s):\n")+(p.Options.Count==0?L.T("  Keep Windows defaults."):string.Join("\n",selected));
  ConfirmLabel.Text=disk==null?L.T("Select a USB device in Installation media first."):L.F($"To confirm this device, type: ERASE DISK {disk.Number}");
  ReviewSummary.Text+=L.T("\n\nApplications: ")+(p.AppSelectionMode=="AfterBoot"?L.T("Choose after first boot"):L.T("Automatically install after first sign-in: ")+string.Join(", ",p.SelectedApps.Select(id=>AppCatalog.Names[id])));
  EraseConsent.IsChecked=false;ConfirmText.Clear();
 }
 void Preview_Click(object s,RoutedEventArgs e)
 {
  try {
   var text=new TextBox {Text=AnswerFile.Generate(Current(),password,true),IsReadOnly=true,FontFamily=new FontFamily("Consolas"),FontSize=12,TextWrapping=TextWrapping.NoWrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(16)};
   new Window {Owner=this,Title=L.T("Generated XML — passwords omitted"),Width=920,Height=650,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=text}.ShowDialog();
  }catch(Exception ex){ShowError(ex);}
 }
 async void Create_Click(object s,RoutedEventArgs e)
 {
  if(busy)return;
  try {
   var p=Current().Clone();p.Validate();
   if(iso==null || !File.Exists(iso))throw new InvalidOperationException(L.T("Select an existing Windows ISO first."));
   if(DiskChoice.SelectedItem is not UsbDisk disk)throw new InvalidOperationException(L.T("Select a USB device first."));
   if(EraseConsent.IsChecked!=true || ConfirmText.Text!=L.F($"ERASE DISK {disk.Number}"))throw new InvalidOperationException(L.T("Confirm data deletion and type the exact device confirmation phrase."));
   // Validate the complete configuration before requesting elevation or creating a job.
   AnswerFile.Generate(p,password);
   SetBusy(true);
   var fresh=(await Services.Disks()).SingleOrDefault(d=>d.Number==disk.Number && d.UniqueId==disk.UniqueId && d.Size==disk.Size);
   if(fresh==null)throw new InvalidOperationException(L.T("The USB device changed. Refresh and select it again."));
   jobDirectory=Services.NewPrivateJob();
   StatusText.Text=L.T("Approve the Windows administrator prompt to validate the ISO and create this USB.");
   await Services.Build(iso,disk,p,password,jobDirectory,new Progress<string>(v=>StatusText.Text=v));
   MessageBox.Show(this,L.T("Installation USB created. Safely eject it before unplugging. Boot the target PC in UEFI mode and select the Windows installation disk there."),L.T("USB ready"),MessageBoxButton.OK,MessageBoxImage.Information);
  }catch(Exception ex){ShowError(ex);StatusText.Text=L.T("Creation did not finish: ")+ex.Message;}
  finally {SetBusy(false);EraseConsent.IsChecked=false;ConfirmText.Clear();LogButton.Visibility=jobDirectory!=null?Visibility.Visible:Visibility.Collapsed;}
 }
 void SetBusy(bool value)
 {
  busy=value;MediaNav.IsEnabled=OptionsNav.IsEnabled=ReviewNav.IsEnabled=CreateButton.IsEnabled=NextButton.IsEnabled=!value;
  MediaPage.IsEnabled=ProfilePage.IsEnabled=OptionsPage.IsEnabled=ReviewPage.IsEnabled=InterfaceLanguageChoice.IsEnabled=!value;
  Progress.Visibility=value?Visibility.Visible:Visibility.Collapsed;Progress.IsIndeterminate=value;
 }
 void Log_Click(object s,RoutedEventArgs e)
 {
  var path=Path.Combine(jobDirectory??"","build.log");if(File.Exists(path))Process.Start(new ProcessStartInfo(path){UseShellExecute=true});else MessageBox.Show(this,L.T("No writer log was created. The administrator prompt may have been canceled."),L.T("Build log"));
 }
 void ShowError(Exception ex)=>MessageBox.Show(this,ex.Message,"WinProvision",MessageBoxButton.OK,MessageBoxImage.Warning);
 internal void VerifyInterface(Action<bool,string> check)
 {
  // Exercise UI transitions without showing the window or enumerating/writing physical disks.
  var original=L.Language;
  profile=new BuildProfile{InstallationProfile=InstallationPresets.Custom,Options=["extensions"],Username="KeepThisName",Language="pt-BR",Keyboard="Brazil ABNT2",AppSelectionMode="BeforeBoot",SelectedApps=["7zip.7zip"]};
  password="KeepThisPassword";
  var testDisk=new UsbDisk{Number=99,FriendlyName="Test USB",UniqueId="TEST-ONLY",Size=16UL*1024*1024*1024,Volumes="X:"};
  DiskChoice.ItemsSource=new List<UsbDisk>{testDisk};DiskChoice.SelectedItem=testDisk;
  foreach(var language in new[]{"pt-BR","en-US"})
  {
   InterfaceLanguageChoice.SelectedIndex=language=="pt-BR"?0:1;
   check(profile.Username=="KeepThisName" && password=="KeepThisPassword" && profile.Options.SetEquals(["extensions"]) && profile.SelectedApps.SetEquals(["7zip.7zip"]),"Language switch lost user choices.");
   check(profile.Language=="pt-BR" && profile.Keyboard=="Brazil ABNT2","UI language changed Windows installation language.");
   check((string)MediaNav.Content==(language=="pt-BR"?"01   Mídia de instalação":"01   Installation media"),"Navigation translation failed.");
   ShowPage(0);SelfTests.RenderInterface((FrameworkElement)Content,"builder-media-"+language,1000,720);
   ShowPage(1);SelfTests.RenderInterface((FrameworkElement)Content,"builder-profiles-"+language,1000,720);
   OpenCustomEditor();
   category="Remove built-in apps";RenderCategories();RenderOptions();
   SelfTests.RenderInterface((FrameworkElement)Content,"builder-custom-"+language,1000,720);
   ShowPage(2);
   check(ReviewSummary.Text.StartsWith(language=="pt-BR"?"Perfil:":"Profile:"),"Review translation failed.");
   check(ConfirmLabel.Text.EndsWith(L.F($"ERASE DISK {99}")) && DiskChoice.SelectedItem==testDisk,"USB selection or confirmation translation changed.");
   SelfTests.RenderInterface((FrameworkElement)Content,"builder-review-"+language,1000,720);
   var applications=new ApplicationsWindow(profile);
   SelfTests.RenderInterface((FrameworkElement)applications.Content,"builder-applications-"+language,650,500);
   applications.Close();
  }
  L.SetLanguage(original);Close();
 }
}
