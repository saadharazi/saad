using System.Diagnostics;
using System.Windows;

namespace SaadApp.Services;

/// <summary>إعادة تشغيل التطبيق (مفيد إذا علق شيء).</summary>
public static class AppRestarter
{
    public static void Restart()
    {
        string? exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
            catch
            {
                // إذا تعذّر التشغيل نغلق فقط
            }
        }

        Application.Current.Shutdown();
    }
}
