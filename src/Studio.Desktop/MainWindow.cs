using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Studio.Core;

namespace Studio.Desktop;
public sealed partial class MainWindow : Window
{
    private readonly ProjectStore store;
    private StudioProject project;
    private DemoEngine engine;
    private readonly ContentControl page=new();
    private readonly TextBlock status=Ui.Text("Leállítva",12,Ui.Muted);
    private readonly TextBlock projectLabel=Ui.Text("",15);
    private readonly TextBlock toast=Ui.Text("",12,Ui.Amber);
    private readonly StackPanel navigation=new();
    private readonly List<Action<EngineSnapshot>> refreshers=[];
    private readonly DispatcherTimer refresh=new(){Interval=TimeSpan.FromMilliseconds(200)};
    private int selected=7,pageIndex,sequenceIndex,stepIndex;private bool dirty,closing,smokeMode;
    private string networkSearch="",logSearch="",logLevel="Összes";private bool onlyFaulty;
    private readonly string[] pages=["Áttekintés","Hálózat","I/O monitor","Vezérlési logika","Eseménynapló","Projektek","Beállítások"];
    private static readonly string[] icons=["\uE80F","\uE968","\uE9D9","\uE8FD","\uE9F9","\uE8B7","\uE713"];
    public static Version CurrentVersion=>Assembly.GetExecutingAssembly().GetName().Version??new(0,1,0);
    public MainWindow(bool smoke=false)
    {
        smokeMode=smoke;Title="SMC Wireless Studio";Width=1500;Height=940;MinWidth=1150;MinHeight=740;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Icon=BitmapFrame.Create(new Uri("pack://application:,,,/Assets/studio.ico"));
        store=new ProjectStore(smoke?Path.Combine(Path.GetTempPath(),"SMCStudio-smoke-"+Guid.NewGuid()):null);
        project=StudioProject.Demo();string? recovery=null;
        var recent=store.Files().FirstOrDefault();if(recent!=null)try{project=store.Load(recent);}catch(Exception ex){recovery="A legutóbbi projekt nem olvasható; új demó nyílt meg. "+ex.Message;}
        engine=new DemoEngine(project);selected=project.Remotes.Any(r=>r.Id==7)?7:project.Remotes[0].Id;
        if(recovery!=null)engine.AddLog("HIBA","Projekt",recovery);
        BuildShell();Navigate(0);
        refresh.Tick+=(_,_)=>Refresh();refresh.Start();
        Closing+=async(_,e)=>{if(closing)return;e.Cancel=true;if(dirty){var answer=MessageBox.Show(this,"Mented a projekt módosításait?","Kilépés",MessageBoxButton.YesNoCancel);if(answer==MessageBoxResult.Cancel)return;if(answer==MessageBoxResult.Yes){try{SaveProject();}catch(Exception ex){MessageBox.Show(ex.Message);return;}}}await engine.StopAsync();PersistLogs();refresh.Stop();closing=true;Close();};
        if(smoke)Loaded+=async(_,_)=>await SmokeTest();
    }
    private void BuildShell()
    {
        var shell=new Grid();shell.RowDefinitions.Add(new(){Height=GridLength.Auto});shell.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});shell.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var top=new DockPanel{LastChildFill=true};
        var tools=Ui.Row(Ui.Button("Projekt mentése",SaveProject),Ui.AsyncButton("Update",CheckUpdates));DockPanel.SetDock(tools,Dock.Right);top.Children.Add(tools);
        top.Children.Add(Ui.Row(Ui.Text("SMC",29,Brushes.White,true),new Border{Width=18},Ui.Text("Wireless Studio",23,null,true),new Border{Width=24},projectLabel,Ui.Badge("DEMÓ · Nincs hardverkapcsolat")));
        shell.Children.Add(new Border{Padding=new Thickness(22,16,14,8),BorderBrush=Ui.Brush("#294357"),BorderThickness=new Thickness(0,0,0,1),Child=top});
        var middle=new Grid();middle.ColumnDefinitions.Add(new(){Width=new GridLength(204)});middle.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});Grid.SetRow(middle,1);shell.Children.Add(middle);
        var side=new DockPanel();var brand=Ui.Stack(Ui.Text("SMC Hungary",14,Ui.Muted),Ui.Text("v"+CurrentVersion.ToString(3)+" · Windows demó",12,Ui.Muted));brand.Margin=new Thickness(20);DockPanel.SetDock(brand,Dock.Bottom);side.Children.Add(brand);navigation.Margin=new Thickness(8,22,6,0);side.Children.Add(navigation);middle.Children.Add(new Border{Background=Ui.Brush("#0E1C29"),Child=side});
        page.Margin=new Thickness(24,20,12,8);Grid.SetColumn(page,1);middle.Children.Add(page);
        var footer=new DockPanel{Margin=new Thickness(20,8,18,8)};DockPanel.SetDock(toast,Dock.Right);footer.Children.Add(toast);footer.Children.Add(status);Grid.SetRow(footer,2);shell.Children.Add(footer);Content=shell;
    }
    private void Navigate(int index)
    {
        pageIndex=index;refreshers.Clear();navigation.Children.Clear();
        for(int i=0;i<pages.Length;i++){int id=i;var label=Ui.Row(new TextBlock{Text=icons[i],FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=18,Width=30,VerticalAlignment=VerticalAlignment.Center},Ui.Text(pages[i],14));var button=Ui.Button(pages[i],()=>Navigate(id),i==index);button.Content=label;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,0,0,9);button.Padding=new Thickness(12,13,4,8);navigation.Children.Add(button);}
        var body=index switch{0=>Overview(),1=>Network(),2=>IoMonitor(),3=>Logic(),4=>EventLog(),5=>Projects(),_=>Settings()};
        page.Content=new ScrollViewer{Content=body};projectLabel.Text=project.Name+(dirty?" *":"");Refresh();
    }
    private StackPanel Page(string title,string subtitle,params UIElement[] content){var s=Ui.Stack(Ui.Text(title,29,null,true),Ui.Text(subtitle,14,Ui.Muted));s.Children.Add(new Border{Height=16});foreach(var x in content)s.Children.Add(x);return s;}
    private void Refresh(){var snapshot=engine.Snapshot();status.Text=$"● DEMÓ  |  {(snapshot.Running?"Fut":"Leállítva")}  |  Cél: {snapshot.CycleMs} ms  |  Mért: {snapshot.ActualMs:F1} ms  |  Maximum: {snapshot.MaxMs:F1} ms  |  Túllépés: {snapshot.Overruns}";foreach(var update in refreshers.ToArray())update(snapshot);}
    private void MarkDirty(){dirty=true;projectLabel.Text=project.Name+" *";}
    private void RequireStopped(){if(engine.Snapshot().Running)throw new InvalidOperationException("Állítsd le a vezérlést a konfiguráció módosításához.");}
    private void SaveProject(){store.Save(project);dirty=false;projectLabel.Text=project.Name;engine.AddLog("INFO","Projekt","Mentve: "+project.Name);toast.Text="Projekt mentve.";}
    private void ApplyEdit(){project.Validate();engine.ApplyConfig(project);MarkDirty();}
    private void PersistLogs(){try{var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SMC Wireless Studio","Logs");if(smokeMode)return;Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"last-session.csv"),DemoEngine.ExportCsv(engine.Logs()),System.Text.Encoding.UTF8);}catch(Exception ex){App.WriteError(ex);}}
    private RemoteConfig Selected=>project.Remotes.First(r=>r.Id==selected);
    private UIElement RunControls()=>Ui.Row(Ui.Button("▶ Indítás",()=>{engine.Start();Navigate(pageIndex);},true),Ui.AsyncButton("■ Leállítás",async()=>{await engine.StopAsync();Navigate(pageIndex);}));
    private UIElement CycleSelector(){var row=new WrapPanel();foreach(var ms in StudioProject.AllowedCycles){var b=Ui.Button(ms+" ms",()=>{RequireStopped();project.CycleMs=ms;ApplyEdit();Navigate(pageIndex);},ms==project.CycleMs);b.ToolTip="Módosítás leállított vezérlésnél";refreshers.Add(s=>b.IsEnabled=!s.Running);row.Children.Add(b);}return row;}
    private Border Stat(string title,Func<EngineSnapshot,string> value){var text=Ui.Text("",26,Ui.Green,true);refreshers.Add(s=>text.Text=value(s));return Ui.Card(text,title);}
    private UIElement Overview()
    {
        var stats=new UniformGrid{Columns=4};stats.Children.Add(Stat("Base állapota",s=>s.Running?"Demó fut":"Készenlét"));stats.Children.Add(Stat("Remote egységek",s=>$"{s.Remotes.Count(r=>r.Online)} / {s.Remotes.Length}"));stats.Children.Add(Stat("Aktív bemenetek",s=>$"{s.Remotes.Sum(r=>r.Inputs.Count(x=>x))} / {s.Remotes.Length*8}"));stats.Children.Add(Stat("Aktív kimenetek",s=>$"{s.Remotes.Sum(r=>r.Outputs.Count(x=>x))} / {s.Remotes.Length*4}"));
        var network=Ui.Card(Ui.Columns(Ui.Stack(Ui.Text("Windows PC",16,null,true),Ui.Text("Ethernet / Modbus TCP",12,Ui.Muted),Ui.Photo("exw1-base",160),Ui.Text(project.BaseName,16,null,true),Ui.Text(project.BaseIp,13,Ui.Muted),Ui.Text("Gyári EXW1 családkép",11,Ui.Muted)),RemoteCards(),0.45),"Wireless hálózat");
        var footer=Ui.Columns(Ui.Card(Ui.Stack(CycleSelector(),Ui.Text("A tényleges ciklusidőt futás közben mérjük.",12,Ui.Muted)),"Ciklusidő"),Ui.Card(RunControls(),"Vezérlés"),2);
        return Page("Rendszeráttekintés","Az összes eszköz egy helyen. Válassz remote-ot a jobb oldali I/O-panelhez.",stats,Ui.Columns(network,Ui.Card(IoPanel(false),"I/O vezérlés"),2.2),footer,Ui.Card(LogRows(4),"Legutóbbi események"));
    }
    private UIElement RemoteCards(){var grid=new UniformGrid{Columns=2};var snapshot=engine.Snapshot();foreach(var r in project.Remotes.Where(r=>r.Name.Contains(networkSearch,StringComparison.CurrentCultureIgnoreCase)||r.Id.ToString().Contains(networkSearch))){if(onlyFaulty&&snapshot.Remotes.First(s=>s.Id==r.Id).Online)continue;var state=Ui.Text("Online",11,Ui.Green);refreshers.Add(s=>{bool on=s.Remotes.First(x=>x.Id==r.Id).Online;state.Text=on?"● Online":"● Offline";state.Foreground=on?Ui.Green:Ui.Amber;});var content=Ui.Columns(Ui.Photo("exw1-remote",55),Ui.Stack(Ui.Text($"R{r.Id:00} · {r.Name}",13,null,true),state),0.25);var b=Ui.Button(r.Name,()=>{selected=r.Id;Navigate(pageIndex);},r.Id==selected);b.Content=content;b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.Padding=new Thickness(6);b.MinHeight=76;b.ToolTip=$"Remote ID: {r.Id} · {r.Name}";grid.Children.Add(b);}return grid;}
    private UIElement Network(){var search=Ui.Input(networkSearch,"Remote keresése");var searchButton=Ui.Button("Keresés",()=>{networkSearch=search.Text.Trim();Navigate(1);});var filter=new CheckBox{Content="Csak offline",IsChecked=onlyFaulty};filter.Click+=(_,_)=>{onlyFaulty=filter.IsChecked==true;Navigate(1);};var name=Ui.Input(Selected.Name,"Eszköz neve");var details=Ui.Card(Ui.Stack(Ui.Photo("exw1-remote",145),Ui.Text($"Remote ID: {selected:00}",16,Ui.Muted),Ui.Field("MEGJELENÍTETT NÉV",name),Ui.Button("Név mentése",()=>{RequireStopped();StudioProject.CheckName(name.Text,"Név");Selected.Name=name.Text.Trim();ApplyEdit();Navigate(1);},true),Ui.Text("Az azonosító és a gyári termékkép megmarad.",12,Ui.Muted),Ui.Button("Online / offline szimuláció",()=>{var r=engine.Snapshot().Remotes.First(r=>r.Id==selected);engine.SetOnline(selected,!r.Online);Navigate(1);}),Ui.Button("+ Demó remote",AddRemote),Ui.Button("Remote eltávolítása",RemoveRemote)),"Kijelölt eszköz");return Page("Hálózat","Eszközök keresése, átnevezése és kapcsolatvesztés szimulálása.",Ui.Columns(Ui.Card(Ui.Stack(search,Ui.Row(searchButton,filter),RemoteCards()),"Remote egységek"),details,2.1));}
    private void AddRemote(){RequireStopped();var id=Enumerable.Range(1,127).FirstOrDefault(x=>project.Remotes.All(r=>r.Id!=x));if(id==0)throw new InvalidOperationException("Elérted a demó 127 egységes határát.");project.Remotes.Add(new(){Id=id,Name="Új remote "+id});selected=id;ApplyEdit();Navigate(1);}
    private void RemoveRemote(){RequireStopped();if(project.Remotes.Count==1)throw new InvalidOperationException("Legalább egy remote szükséges.");if(project.Sequences.Any(s=>s.Steps.Any(x=>x.RemoteId==selected)))throw new InvalidOperationException("A remote-ra vezérlési lépés hivatkozik. Először módosítsd a lépést.");project.Remotes.Remove(Selected);selected=project.Remotes[0].Id;ApplyEdit();Navigate(1);}
}
