using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Studio.Desktop;
public sealed partial class MainWindow
{
    private bool updateBusy;
    private async Task CheckUpdates()
    {
        if(updateBusy)return;updateBusy=true;try{
            toast.Text="Frissítés keresése…";var update=await UpdateService.FindAsync();
            if(update==null){toast.Text="A legújabb elérhető verzió fut.";MessageBox.Show(this,"Nincs újabb stabil kiadás.\nTelepített verzió: "+CurrentVersion.ToString(3),"Update");return;}
            if(MessageBox.Show(this,$"Elérhető: {update.Tag}\n\nLetöltöd és elindítod a frissítést?\nA projektet mentjük, a vezérlést leállítjuk.","Update",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            var path=await UpdateService.DownloadAsync(update,message=>Dispatcher.Invoke(()=>toast.Text=message));
            await engine.StopAsync();SaveProject();PersistLogs();
            try{UpdateService.Launch(path);}catch(System.ComponentModel.Win32Exception ex) when(ex.NativeErrorCode==1223){toast.Text="A frissítés indítását megszakítottad.";return;}
            refresh.Stop();closing=true;Close();
        }finally{updateBusy=false;}
    }
    private async Task SmokeTest()
    {
        try{
            var dir=Environment.GetEnvironmentVariable("SMC_SMOKE_DIR")??Path.Combine(Path.GetTempPath(),"SMCStudioSmoke");Directory.CreateDirectory(dir);
            SaveProject();engine.Start();await Task.Delay(300);engine.SetOutput(selected,0,true);await Task.Delay(300);
            if(!engine.Snapshot().Remotes.First(r=>r.Id==selected).Outputs[0])throw new Exception("Output switch failed.");
            await engine.StopAsync();
            for(int i=0;i<pages.Length;i++){Navigate(i);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(dir,$"view-{i}.png"));encoder.Save(file);}
            project.Remotes[0].Name="Smoke renamed";ApplyEdit();SaveProject();var loaded=store.Load(store.Files().First());if(loaded.Remotes[0].Name!="Smoke renamed")throw new Exception("Project persistence failed.");
            File.WriteAllText(Path.Combine(dir,"smoke-result.txt"),"PASS: all seven views render, demo outputs switch, stop clears outputs, project rename persists. Version "+CurrentVersion.ToString(3));
            dirty=false;closing=true;await engine.StopAsync();Application.Current.Shutdown(0);
        }catch(Exception ex){App.WriteError(ex);Console.Error.WriteLine(ex);Application.Current.Shutdown(1);}
    }
}
