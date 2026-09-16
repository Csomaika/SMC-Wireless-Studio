using Studio.Core;
using System.Diagnostics;
static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
static void Throws(Action action,string message){try{action();}catch(InvalidDataException){Console.WriteLine("PASS: "+message);return;}throw new Exception(message);}
var root=Path.Combine(Path.GetTempPath(),"SMC-CoreTests-"+Guid.NewGuid());
try{
 var store=new ProjectStore(root);var project=StudioProject.Demo();project.Remotes[0].Name="Őrlő állomás";project.Remotes[0].Inputs[0]="Munkadarab – érzékelő";
 var path=store.Save(project);var loaded=store.Load(path);Check(loaded.Remotes[0].Name==project.Remotes[0].Name&&loaded.Remotes[0].Inputs[0]==project.Remotes[0].Inputs[0],"Hungarian names round-trip");
 var old=File.ReadAllText(path);project.CycleMs=3;Throws(()=>store.Save(project),"Invalid cycle rejected");Check(File.ReadAllText(path)==old,"Failed save preserves previous project");project.CycleMs=20;
 project.Remotes[1].Id=1;Throws(project.Validate,"Duplicate IDs rejected");project.Remotes[1].Id=2;
 project.Sequences[0].Steps[0].RemoteId=99;Throws(project.Validate,"Dangling sequence target rejected");project.Sequences[0].Steps[0].RemoteId=7;
 Check(ReleaseVersion.IsNewer("v0.2.0",new Version(0,1,0,0)),"New release detected");Check(!ReleaseVersion.IsNewer("v0.1.0",new Version(0,1,0,0)),"Same version not reinstalled");Check(!ReleaseVersion.IsNewer("v0.0.9",new Version(0,1,0)),"Downgrade not offered");
 Check(ReleaseVersion.IsTrustedAsset("https://github.com/Csomaika/SMC-Wireless-Studio/releases/download/v0.2.0/app.msi"),"Own release asset accepted");Check(!ReleaseVersion.IsTrustedAsset("https://github.com.attacker.test/Csomaika/SMC-Wireless-Studio/releases/download/v0.2.0/app.msi"),"Lookalike host rejected");Check(!ReleaseVersion.IsTrustedAsset("https://github.com/attacker/other/releases/download/v0.2.0/app.msi"),"Other repository rejected");
 await using(var engine=new DemoEngine(project)){engine.Start();await Task.Delay(120);engine.SetOutput(7,0,true);Check(engine.Snapshot().Remotes.First(r=>r.Id==7).Outputs[0],"Manual output switches on");await engine.StopAsync();Check(engine.Snapshot().Remotes.All(r=>r.Outputs.All(v=>!v)),"Stop clears every output");Check(engine.Snapshot().Cycles>0,"Background cycle runs");engine.Start();engine.StartSequence(0);var watch=Stopwatch.StartNew();while(engine.Snapshot().SequenceRunning&&watch.ElapsedMilliseconds<5000)await Task.Delay(30);Check(engine.Snapshot().SequenceStatus=="Befejezve","Sample sequence completes");Check(!engine.Snapshot().Remotes.First(r=>r.Id==7).Outputs[0],"Sample sequence releases output");await engine.StopAsync();}
 var timeout=project.Clone();timeout.Sequences[0].Steps=[new(){Kind=StepKind.SetOutput,RemoteId=7,Channel=0,Value=true},new(){Kind=StepKind.WaitInput,RemoteId=7,Channel=7,Value=true,TimeoutMs=60}];
 await using(var engine=new DemoEngine(timeout)){engine.AutomaticSensors=false;engine.Start();engine.StartSequence(0);await Task.Delay(350);Check(engine.Snapshot().SequenceStatus.StartsWith("Hiba:"),"Input timeout aborts sequence");Check(engine.Snapshot().Remotes.All(r=>r.Outputs.All(v=>!v)),"Timeout clears all outputs");await engine.StopAsync();}
 var csv=DemoEngine.ExportCsv([new(DateTime.Now,"INFO","=HYPERLINK()","quote \" ; newline\ntext")]);Check(csv.Contains("'=HYPERLINK()")&&csv.Contains("\"\""),"CSV quoting and formula neutralization");
 Console.WriteLine("All core tests passed.");
}finally{Directory.Delete(root,true);}
