using System.IO;
using System.Windows;
namespace Studio.Desktop;
public partial class App : Application
{
    private Mutex? singleton;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var smoke=e.Args.Contains("--smoke-test");
        singleton=new Mutex(true,"Local\\SMCWirelessStudio",out var first);
        if(!first&&!smoke){MessageBox.Show("Az SMC Wireless Studio már fut.");Shutdown();return;}
        DispatcherUnhandledException+=(s,args)=>{WriteError(args.Exception);MessageBox.Show("Váratlan hiba. A részleteket a hibajegyzékbe mentettük.\n"+args.Exception.Message,"SMC Wireless Studio");args.Handled=true;Shutdown(1);};
        try {var window=new MainWindow(smoke);MainWindow=window;window.Show();}
        catch(Exception ex){WriteError(ex);if(!smoke)MessageBox.Show("Az indítás nem sikerült: "+ex.Message);Shutdown(1);}
    }
    internal static void WriteError(Exception ex){try{var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SMC Wireless Studio","Logs");Directory.CreateDirectory(dir);File.AppendAllText(Path.Combine(dir,"errors.log"),DateTime.Now.ToString("O")+" "+ex+Environment.NewLine);}catch{}}
    protected override void OnExit(ExitEventArgs e){singleton?.Dispose();base.OnExit(e);}
}
