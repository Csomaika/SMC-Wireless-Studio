using System.Text.Json;

namespace Studio.Core;

public sealed class RemoteConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = "Remote";
    public string[] Inputs { get; set; } = ["Munkadarab jelen", "Henger alaphelyzet", "Henger véghelyzet", "Nyomás rendben", "Tartalék 1", "Tartalék 2", "Tartalék 3", "Tartalék 4"];
    public string[] Outputs { get; set; } = ["Szelep A", "Szelep B", "Jelzőlámpa", "Tartalék"];
}

public enum StepKind { WaitInput, SetOutput, Delay }
public sealed class LogicStep
{
    public string Name { get; set; } = "Új lépés";
    public StepKind Kind { get; set; }
    public int RemoteId { get; set; } = 7;
    public int Channel { get; set; }
    public bool Value { get; set; } = true;
    public int DurationMs { get; set; } = 500;
    public int TimeoutMs { get; set; } = 3000;
}
public sealed class SequenceConfig
{
    public string Name { get; set; } = "Megfogási ciklus";
    public List<LogicStep> Steps { get; set; } = [];
}
public sealed class StudioProject
{
    public int SchemaVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Wireless tesztpad";
    public string Customer { get; set; } = "SMC Hungary";
    public string BaseName { get; set; } = "EXW1 Base";
    public string BaseIp { get; set; } = "192.168.0.10";
    public int Port { get; set; } = 502;
    public int CycleMs { get; set; } = 20;
    public List<RemoteConfig> Remotes { get; set; } = [];
    public List<SequenceConfig> Sequences { get; set; } = [];
    public static readonly int[] AllowedCycles = [10,20,50,100];

    public static StudioProject Demo()
    {
        string[] names = ["Betáplálás", "Munkadarab", "Pozicionálás", "Nyomásfigyelés", "Átadó", "Szerelőállomás", "Megfogó", "Ellenőrzés", "Kiadás", "Jelzések"];
        return new() {
            Remotes = names.Select((name,index) => new RemoteConfig { Id=index+1,Name=name }).ToList(),
            Sequences = [new() { Steps = [
                new() {Name="Várakozás munkadarabra",Kind=StepKind.WaitInput,Channel=0,TimeoutMs=10000},
                new() {Name="Megfogó zárása",Kind=StepKind.SetOutput,Channel=0},
                new() {Name="Véghelyzet ellenőrzése",Kind=StepKind.WaitInput,Channel=2},
                new() {Name="Tartási idő",Kind=StepKind.Delay,DurationMs=500},
                new() {Name="Megfogó nyitása",Kind=StepKind.SetOutput,Channel=0,Value=false}
            ] }]
        };
    }
    public void Validate()
    {
        if(SchemaVersion!=1) throw new InvalidDataException("Nem támogatott projektverzió.");
        if(Id==Guid.Empty) throw new InvalidDataException("Hiányzó projektazonosító.");
        CheckName(Name,"Projektnév"); CheckName(Customer,"Ügyfél"); CheckName(BaseName,"Base név");
        if(!System.Net.IPAddress.TryParse(BaseIp,out var ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork) throw new InvalidDataException("Érvénytelen IPv4-cím.");
        if(Port<1||Port>65535) throw new InvalidDataException("A port 1–65535 közötti lehet.");
        if(!AllowedCycles.Contains(CycleMs)) throw new InvalidDataException("A ciklus 10, 20, 50 vagy 100 ms lehet.");
        if(Remotes is null || Remotes.Count is <1 or >127) throw new InvalidDataException("1–127 demo remote engedélyezett.");
        if(Remotes.Any(r=>r is null)||Remotes.Select(r=>r.Id).Distinct().Count()!=Remotes.Count) throw new InvalidDataException("Ismétlődő vagy hibás remote.");
        foreach(var r in Remotes) {
            if(r.Id is <1 or >127) throw new InvalidDataException("Érvénytelen remote azonosító.");
            CheckName(r.Name,"Remote név");
            if(r.Inputs is null||r.Inputs.Length!=8||r.Outputs is null||r.Outputs.Length!=4) throw new InvalidDataException("A demó 8 bemenetet és 4 kimenetet kezel remote-onként.");
            foreach(var s in r.Inputs.Concat(r.Outputs)) CheckName(s,"Jelnév");
        }
        if(Sequences is null||Sequences.Count>100) throw new InvalidDataException("Hibás szekvencialista.");
        foreach(var seq in Sequences) {
            if(seq is null) throw new InvalidDataException("Hibás szekvencia.");
            CheckName(seq.Name,"Szekvencianév");
            if(seq.Steps is null||seq.Steps.Count is <1 or >200) throw new InvalidDataException("Egy szekvencia 1–200 lépést tartalmazhat.");
            foreach(var step in seq.Steps) {
                if(step is null||!Enum.IsDefined(step.Kind)) throw new InvalidDataException("Hibás lépéstípus.");
                CheckName(step.Name,"Lépésnév");
                if(!Remotes.Any(r=>r.Id==step.RemoteId)) throw new InvalidDataException("A lépés nem létező remote-ra hivatkozik.");
                if(step.Channel<0||step.Channel>=(step.Kind==StepKind.SetOutput?4:8)) throw new InvalidDataException("Hibás csatornaszám.");
                if(step.DurationMs is <0 or >3600000||step.TimeoutMs is <10 or >3600000) throw new InvalidDataException("Érvénytelen időzítés.");
            }
        }
    }
    public static void CheckName(string? value,string title) { if(string.IsNullOrWhiteSpace(value)||value.Length>100||value.Any(char.IsControl)) throw new InvalidDataException(title+": 1–100 látható karakter szükséges."); }
    public StudioProject Clone() => JsonSerializer.Deserialize<StudioProject>(JsonSerializer.Serialize(this))!;
}

public sealed class ProjectStore
{
    public string Root { get; }
    private static readonly JsonSerializerOptions Json = new() { WriteIndented=true,PropertyNameCaseInsensitive=true };
    public ProjectStore(string? root=null) { Root=root??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SMC Wireless Studio","Projects");Directory.CreateDirectory(Root); }
    public string Save(StudioProject project) { project.Validate();var path=Path.Combine(Root,project.Id+".smcws.json");WriteAtomic(path,JsonSerializer.Serialize(project,Json));return path; }
    public StudioProject Load(string path) {
        var info=new FileInfo(path);if(info.Length>5_000_000)throw new InvalidDataException("A projektfájl túl nagy.");
        var p=JsonSerializer.Deserialize<StudioProject>(File.ReadAllText(path),Json)??throw new InvalidDataException("Üres projektfájl.");p.Validate();return p;
    }
    public IEnumerable<string> Files() => Directory.EnumerateFiles(Root,"*.smcws.json").OrderByDescending(File.GetLastWriteTimeUtc);
    public void Export(StudioProject project,string path) { project.Validate();WriteAtomic(path,JsonSerializer.Serialize(project,Json)); }
    public static void WriteAtomic(string path,string content) {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllText(temp,content);File.Move(temp,path,true);} finally {if(File.Exists(temp))File.Delete(temp);}
    }
}
