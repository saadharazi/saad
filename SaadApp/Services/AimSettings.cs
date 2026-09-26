using System.IO;
using System.Text.Json;

namespace SaadApp.Services;

/// <summary>
/// إعدادات الشاشة الرئيسية، تُحفظ على الجهاز وتُسترجع عند فتح التطبيق.
/// </summary>
public sealed class AimSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BoostStore", "settings.json");

    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = "Balanced";
    public double Strength { get; set; } = 75;
    public double Smoothness { get; set; } = 40;
    public double Fov { get; set; } = 120;
    public double ReactionMs { get; set; } = 15;
    public bool ShowFov { get; set; } = true;
    public bool Prediction { get; set; } = true;
    public bool ClosestTarget { get; set; }
    public string Target { get; set; } = "Head";

    /// <summary>إبقاء نافذة التطبيق فوق كل النوافذ.</summary>
    public bool AlwaysOnTop { get; set; }

    public static AimSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AimSettings>(File.ReadAllText(FilePath)) ?? new AimSettings();
        }
        catch
        {
            // ملف تالف -> الإعدادات الافتراضية
        }

        return new AimSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // الحفظ اختياري
        }
    }
}
