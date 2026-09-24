using System.Globalization;
using System.Text.Json;

namespace SaadApp.Services;

public sealed record UpdateInfo(double Version, string Url);

/// <summary>
/// يقرأ عقدة "update" من قاعدة البيانات ويقارن "Update number" برقم هذه النسخة.
/// </summary>
public static class UpdateService
{
    /// <summary>يرجع معلومات التحديث إذا كانت هناك نسخة أحدث، وإلا null.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        JsonElement? node = await FirebaseClient.GetAsync("update", ct).ConfigureAwait(false);
        if (node is not { ValueKind: JsonValueKind.Object } update)
            return null;

        double serverVersion = ReadNumber(update, "Update number");
        string url = update.TryGetProperty("Update URL", out var u) && u.ValueKind == JsonValueKind.String
            ? u.GetString() ?? ""
            : "";

        return serverVersion > AppConfig.CurrentVersion ? new UpdateInfo(serverVersion, url) : null;
    }

    private static double ReadNumber(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
            return 0;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double d) => d,
            _ => 0,
        };
    }
}
