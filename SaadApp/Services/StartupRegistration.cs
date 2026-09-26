using Microsoft.Win32;

namespace SaadApp.Services;

/// <summary>
/// التشغيل مع ويندوز عبر مفتاح Run للمستخدم الحالي (لا يحتاج صلاحيات مسؤول).
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BoostStore";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) is string;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>يفعّل أو يلغي التشغيل مع ويندوز. يرجع false إذا فشلت العملية.</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                string? exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                    return false;
                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
