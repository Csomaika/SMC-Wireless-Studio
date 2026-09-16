using System.Diagnostics;
namespace Studio.Core;

public sealed record LogEntry(DateTime Time,string Level,string Device,string Message);
public sealed record RemoteSnapshot(int Id,bool Online,bool[] Inputs,bool[] Outputs);
public sealed record EngineSnapshot(bool Running,int CycleMs,double ActualMs,double MaxMs,long Overruns,long Cycles,bool SequenceRunning,int StepIndex,string SequenceStatus,RemoteSnapshot[] Remotes);

public sealed class DemoEngine : IAsyncDisposable
{
    private sealed class State { public bool Online=true;public bool[] Inputs=new bool[8];public bool[] Outputs=new bool[4];public long ChangedAt; }
    private readonly object gate=new(); private readonly Dictionary<int,State> states=[];
    private readonly List<LogEntry> logs=[];
    private StudioProject project;private CancellationTokenSource? cancellation;private Task? worker;
    private bool running;private bool automaticSensors=true;private int cycleMs;
    private double actual,max;private long overruns,cycles;private long lastTick;
    private SequenceConfig? active;private int stepIndex=-1;private long stepStarted;private string status="Leállítva";
    public DemoEngine(StudioProject project) {project.Validate();this.project=project.Clone();cycleMs=project.CycleMs;foreach(var r in project.Remotes)states[r.Id]=new();AddLog("INFO","Rendszer","Demómód: nincs kapcsolat valódi hardverhez.");}
    public bool AutomaticSensors {get{lock(gate)return automaticSensors;}set{lock(gate){automaticSensors=value;AddLog("INFO","Szimulátor",value?"Automatikus szenzorok bekapcsolva.":"Kézi bemeneti szimuláció.");}}}
    public void AddLog(string level,string device,string message) {lock(gate){logs.Insert(0,new(DateTime.Now,level,device,message));if(logs.Count>2000)logs.RemoveRange(2000,logs.Count-2000);}}
    public LogEntry[] Logs() {lock(gate)return logs.ToArray();}
    public void ApplyConfig(StudioProject value) {value.Validate();lock(gate){if(running)throw new InvalidOperationException("Állítsd le a vezérlést a módosításhoz.");project=value.Clone();cycleMs=value.CycleMs;foreach(var id in states.Keys.Except(value.Remotes.Select(r=>r.Id)).ToArray())states.Remove(id);foreach(var r in value.Remotes)states.TryAdd(r.Id,new());active=null;stepIndex=-1;status="Leállítva";}}
    public void Start() {lock(gate){if(running)return;if(worker is {IsCompleted:false})throw new InvalidOperationException("A leállítás még folyamatban van.");cancellation?.Dispose();cancellation=new();running=true;actual=max=0;cycles=overruns=0;lastTick=0;AddLog("INFO","Vezérlés",$"Demó indítva · {cycleMs} ms célciklus.");worker=Task.Run(()=>RunAsync(cancellation.Token));}}
    public async Task StopAsync() {Task? pending;lock(gate){running=false;cancellation?.Cancel();active=null;status="Leállítva";stepIndex=-1;foreach(var s in states.Values)Array.Clear(s.Outputs);pending=worker;}if(pending!=null){try{await pending;}catch(OperationCanceledException){}}AddLog("INFO","Vezérlés","Leállítva. Demókimenetek kikapcsolva.");}
    private async Task RunAsync(CancellationToken token) {
        try{using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(cycleMs));while(await timer.WaitForNextTickAsync(token)){lock(gate){if(!running)break;Tick(Stopwatch.GetTimestamp());}}}
        catch(OperationCanceledException) when(token.IsCancellationRequested){}
        catch(Exception ex){lock(gate){running=false;Fail("Vezérlési hiba: "+ex.Message);}}
    }
    private void Tick(long now) {
        if(lastTick!=0){actual=Stopwatch.GetElapsedTime(lastTick,now).TotalMilliseconds;max=Math.Max(max,actual);if(actual>cycleMs){overruns++;if(overruns==1||overruns%100==0)AddLog("FIGYELEM","Időzítés",$"Ciklus {actual:F1} ms, cél {cycleMs} ms; túllépések: {overruns}.");}}lastTick=now;cycles++;
        foreach(var s in states.Values)if(s.Online&&automaticSensors){s.Inputs[0]=true;s.Inputs[3]=true;if(Stopwatch.GetElapsedTime(s.ChangedAt,now).TotalMilliseconds>=250){s.Inputs[1]=!s.Outputs[0];s.Inputs[2]=s.Outputs[0];}}
        if(active==null)return;
        var step=active.Steps[stepIndex];var state=states[step.RemoteId];
        if(!state.Online){Fail("Kapcsolatvesztés a szekvenciában.");return;}
        var elapsed=Stopwatch.GetElapsedTime(stepStarted,now).TotalMilliseconds;bool done=false;
        if(step.Kind==StepKind.SetOutput){state.Outputs[step.Channel]=step.Value;state.ChangedAt=now;done=true;AddLog("INFO",$"R{step.RemoteId:00}",$"DO{step.Channel:00}: {(step.Value?"BE":"KI")} · {step.Name}");}
        else if(step.Kind==StepKind.Delay)done=elapsed>=step.DurationMs;
        else {done=state.Inputs[step.Channel]==step.Value;if(!done&&elapsed>=step.TimeoutMs){Fail("Bemeneti feltétel időtúllépése: "+step.Name);return;}}
        if(done){stepIndex++;stepStarted=now;if(stepIndex>=active.Steps.Count){status="Befejezve";active=null;stepIndex=-1;AddLog("INFO","Szekvencia","A lépéssor befejeződött.");}else status=active.Steps[stepIndex].Name;}
    }
    private void Fail(string message){active=null;stepIndex=-1;status="Hiba: "+message;foreach(var s in states.Values)Array.Clear(s.Outputs);AddLog("HIBA","Szekvencia",message+" Demókimenetek kikapcsolva.");}
    public void StartSequence(int index) {lock(gate){if(!running)throw new InvalidOperationException("Először indítsd el a demóvezérlést.");if(active!=null)throw new InvalidOperationException("Már fut egy szekvencia.");if(index<0||index>=project.Sequences.Count)throw new InvalidOperationException("Nincs kiválasztott szekvencia.");project.Validate();active=project.Sequences[index];stepIndex=0;stepStarted=Stopwatch.GetTimestamp();status=active.Steps[0].Name;AddLog("INFO","Szekvencia",active.Name+" elindítva.");}}
    public void SetOutput(int id,int channel,bool value){lock(gate){if(!running)throw new InvalidOperationException("Először indítsd el a demóvezérlést.");if(active!=null)throw new InvalidOperationException("Kézi kapcsolás előtt állítsd le a szekvenciát.");var s=states[id];if(!s.Online)throw new InvalidOperationException("Az eszköz offline.");s.Outputs[channel]=value;s.ChangedAt=Stopwatch.GetTimestamp();AddLog("INFO",$"R{id:00}",$"DO{channel:00}: {(value?"BE":"KI")} · kézi kapcsolás");}}
    public void SetInput(int id,int channel,bool value){lock(gate){automaticSensors=false;states[id].Inputs[channel]=value;}}
    public void SetOnline(int id,bool value){lock(gate){states[id].Online=value;if(!value){Array.Clear(states[id].Outputs);Array.Clear(states[id].Inputs);if(active!=null)Fail("Remote kapcsolatvesztés: "+id);}AddLog(value?"INFO":"FIGYELEM",$"R{id:00}",value?"Szimulált kapcsolat helyreállítva.":"Szimulált kapcsolatvesztés.");}}
    public EngineSnapshot Snapshot(){lock(gate)return new(running,cycleMs,actual,max,overruns,cycles,active!=null,stepIndex,status,states.Select(x=>new RemoteSnapshot(x.Key,x.Value.Online,(bool[])x.Value.Inputs.Clone(),(bool[])x.Value.Outputs.Clone())).ToArray());}
    public async ValueTask DisposeAsync(){await StopAsync();cancellation?.Dispose();}
    public static string ExportCsv(IEnumerable<LogEntry> entries){static string Q(string x){if(x.Length>0&&"=+-@\t\r".Contains(x[0]))x="'"+x;return "\""+x.Replace("\"","\"\"")+"\"";}return "Időpont;Szint;Eszköz;Esemény\r\n"+string.Join("\r\n",entries.Select(e=>string.Join(';',Q(e.Time.ToString("O")),Q(e.Level),Q(e.Device),Q(e.Message))));}
}
