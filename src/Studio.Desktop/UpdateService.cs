using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Studio.Core;
namespace Studio.Desktop;
internal sealed record AvailableUpdate(string Tag,string Url,string HashUrl);
internal static class UpdateService
{
    private static HttpClient Client(){var c=new HttpClient{Timeout=TimeSpan.FromMinutes(10)};c.DefaultRequestHeaders.UserAgent.ParseAdd("SMCWirelessStudio/"+MainWindow.CurrentVersion.ToString(3));return c;}
    public static async Task<AvailableUpdate?> FindAsync()
    {
        using var client=Client();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));using var response=await client.GetAsync("https://api.github.com/repos/"+ReleaseVersion.Repository+"/releases/latest",timeout.Token);
        if(response.StatusCode==HttpStatusCode.NotFound)return null;response.EnsureSuccessStatusCode();
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));var root=doc.RootElement;
        if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())return null;
        var tag=root.GetProperty("tag_name").GetString()??"";if(!ReleaseVersion.IsNewer(tag,MainWindow.CurrentVersion))return null;
        var assets=root.GetProperty("assets").EnumerateArray().ToArray();var installer=assets.FirstOrDefault(x=>x.GetProperty("name").GetString() is string n&&n.StartsWith("SMC-Wireless-Studio-",StringComparison.Ordinal)&&n.EndsWith("-x64.msi",StringComparison.Ordinal));
        if(installer.ValueKind==JsonValueKind.Undefined)throw new InvalidDataException("Az új kiadás nem tartalmaz x64 MSI-t.");
        var filename=installer.GetProperty("name").GetString()!;var hash=assets.FirstOrDefault(x=>x.GetProperty("name").GetString()==filename+".sha256");
        if(hash.ValueKind==JsonValueKind.Undefined)throw new InvalidDataException("A kiadás SHA-256 ellenőrzőfájlja hiányzik.");
        var url=installer.GetProperty("browser_download_url").GetString()!;var hashUrl=hash.GetProperty("browser_download_url").GetString()!;
        if(!ReleaseVersion.IsTrustedAsset(url)||!ReleaseVersion.IsTrustedAsset(hashUrl))throw new InvalidDataException("A frissítés forrása nem a projekt kiadási oldala.");
        return new(tag,url,hashUrl);
    }
    public static async Task<string> DownloadAsync(AvailableUpdate update,Action<string> progress)
    {
        using var client=Client();var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SMC Wireless Studio","Updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        var target=Path.Combine(dir,"SMC-Wireless-Studio-Update.msi");var partial=target+".partial";
        try{
            var checksum=(await client.GetStringAsync(update.HashUrl)).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";
            if(checksum.Length!=64||checksum.Any(ch=>!Uri.IsHexDigit(ch)))throw new InvalidDataException("Hibás ellenőrzőösszeg.");
            using var response=await client.GetAsync(update.Url,HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
            const long limit=500L*1024*1024;var total=response.Content.Headers.ContentLength;if(total>limit)throw new InvalidDataException("Túl nagy telepítő.");
            await using(var input=await response.Content.ReadAsStreamAsync())await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true)){
                var buffer=new byte[81920];long received=0;int n;long last=0;while((n=await input.ReadAsync(buffer))>0){received+=n;if(received>limit)throw new InvalidDataException("Túl nagy telepítő.");await output.WriteAsync(buffer.AsMemory(0,n));if(received-last>1024*1024){last=received;progress(total>0?$"Frissítés letöltése: {100*received/total}%":$"Frissítés: {received/1024/1024} MB");}}
            }
            using(var file=File.OpenRead(partial)){var actual=Convert.ToHexString(await SHA256.HashDataAsync(file));if(!actual.Equals(checksum,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Az MSI ellenőrzőösszege eltér. A frissítés nem indul el.");}
            File.Move(partial,target);return target;
        }catch{if(File.Exists(partial))File.Delete(partial);throw;}
    }
    public static void Launch(string path){var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe")){UseShellExecute=true,Verb="runas",Arguments="/i \""+path+"\""};_ = Process.Start(info)??throw new InvalidOperationException("A telepítő nem indult el.");}
}
