using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SaadApp.Services;

/// <summary>
/// حفظ الكود على الجهاز (خيار "تذكرني") مشفّراً بـ DPAPI لمستخدم ويندوز الحالي.
/// </summary>
public static class RememberedKeyStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BoostStore", "session.dat");

    public static string? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            byte[] data = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string key)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, data);
        }
        catch
        {
            // الحفظ اختياري
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
        catch
        {
            // تجاهل
        }
    }
}
