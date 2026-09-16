namespace Studio.Core;
public static class ReleaseVersion
{
    public const string Repository="Csomaika/SMC-Wireless-Studio";
    public static bool TryParse(string? text,out Version version){version=new Version(0,0,0);if(string.IsNullOrWhiteSpace(text))return false;return Version.TryParse(text.TrimStart('v','V'),out version!);}
    public static bool IsNewer(string tag,Version installed)=>TryParse(tag,out var candidate)&&candidate>new Version(installed.Major,installed.Minor,Math.Max(0,installed.Build));
    public static bool IsTrustedAsset(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="github.com"&&uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo)&&uri.AbsolutePath.StartsWith("/"+Repository+"/releases/download/",StringComparison.Ordinal)&&string.IsNullOrEmpty(uri.Query);
}
