using System.Diagnostics;

namespace SaadApp.Services;

public static class UrlLauncher
{
    /// <summary>يفتح رابط https في المتصفح. يرجع false إذا كان الرابط فارغاً أو غير صالح.</summary>
    public static bool Open(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
