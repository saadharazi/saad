using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SaadApp.Services;

/// <summary>بيانات اشتراك صالح.</summary>
public sealed record LicenseInfo(string Key, SubscriptionPlan Plan, DateTime StartUtc, string UserName)
{
    /// <summary>وقت الانتهاء (null = مدى الحياة).</summary>
    public DateTime? ExpiresUtc => Plan.IsLifetime ? null : StartUtc + Plan.Duration;

    public TimeSpan? Remaining(DateTime nowUtc) =>
        ExpiresUtc is { } expires ? (expires > nowUtc ? expires - nowUtc : TimeSpan.Zero) : null;

    public bool IsExpired(DateTime nowUtc) => ExpiresUtc is { } expires && nowUtc >= expires;
}

public enum LicenseStatus
{
    Valid,
    InvalidFormat,
    NotFound,
    Disabled,
    InvalidType,
    OtherDevice,
    Expired,
    ActivationDenied,
    AccessDenied,
    NetworkError,
}

public sealed record LicenseResult(LicenseStatus Status, LicenseInfo? License = null, string? Detail = null)
{
    public bool IsValid => Status == LicenseStatus.Valid;

    /// <summary>خطأ اتصال مؤقت (ليس رفضاً من الخادم).</summary>
    public bool IsNetworkError => Status is LicenseStatus.NetworkError or LicenseStatus.AccessDenied;

    /// <summary>الرسالة مع تفاصيل تقنية (رمز الخطأ) إن وُجدت.</summary>
    public string Message => Detail is null ? BaseMessage : $"{BaseMessage} ({Detail})";

    private string BaseMessage => Status switch
    {
        LicenseStatus.Valid => "",
        LicenseStatus.InvalidFormat => "صيغة الكود غير صحيحة",
        LicenseStatus.NotFound => "الكود غير صحيح",
        LicenseStatus.Disabled => "هذا الكود موقوف، تواصل مع الدعم الفني",
        LicenseStatus.InvalidType => "نوع الاشتراك غير محدد لهذا الكود، تواصل مع الدعم الفني",
        LicenseStatus.OtherDevice => "هذا الكود مفعّل على جهاز آخر",
        LicenseStatus.Expired => "انتهت صلاحية هذا الكود",
        LicenseStatus.ActivationDenied => "تعذّر حفظ تفعيل الكود في قاعدة البيانات، تواصل مع الدعم الفني",
        LicenseStatus.AccessDenied => "قاعدة البيانات رفضت الاتصال، تحقق من قواعد الحماية (Rules)",
        LicenseStatus.NetworkError => "تعذّر الاتصال بالخادم، تحقق من الإنترنت",
        _ => "خطأ غير معروف",
    };
}

/// <summary>
/// التحقق من الكود وتفعيله على الجهاز.
/// الشكل في قاعدة البيانات: users/{الكود}/{device_id, subscription_active, subscription_date, subscription_type, user_name}
/// </summary>
public static partial class LicenseService
{
    public const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd", "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd",
    ];

    /// <summary>
    /// يتحقق من الكود، وإذا كان جديداً (device_id فارغ) يربطه بهذا الجهاز ويسجل تاريخ البداية.
    /// </summary>
    public static Task<LicenseResult> ActivateAsync(string key, CancellationToken ct = default) =>
        EvaluateAsync(key, allowActivation: true, ct);

    /// <summary>يتحقق من الكود بدون تفعيل (للمراقبة أثناء التشغيل).</summary>
    public static Task<LicenseResult> ValidateAsync(string key, CancellationToken ct = default) =>
        EvaluateAsync(key, allowActivation: false, ct);

    public static bool IsValidKeyFormat(string key) => KeyPattern().IsMatch(key);

    private static async Task<LicenseResult> EvaluateAsync(string key, bool allowActivation, CancellationToken ct)
    {
        key = key.Trim();
        if (!IsValidKeyFormat(key))
            return new LicenseResult(LicenseStatus.InvalidFormat);

        string path = $"users/{key}";

        try
        {
            JsonElement? node = await FirebaseClient.GetAsync(path, ct).ConfigureAwait(false);
            if (node is not { ValueKind: JsonValueKind.Object } record)
                return new LicenseResult(LicenseStatus.NotFound);

            string deviceId = ReadString(record, "device_id");

            if (deviceId.Length == 0)
            {
                if (!allowActivation)
                    return new LicenseResult(LicenseStatus.OtherDevice);

                // نتحقق قبل التفعيل حتى لا نربط كوداً موقوفاً أو بدون نوع
                var precheck = Check(key, record, ignoreDevice: true);
                if (!precheck.IsValid)
                    return precheck;

                string? writeError = await TryWriteActivationAsync(path, ct).ConfigureAwait(false);

                node = await FirebaseClient.GetAsync(path, ct).ConfigureAwait(false);
                if (node is not { ValueKind: JsonValueKind.Object } refreshed)
                    return new LicenseResult(LicenseStatus.NotFound);

                record = refreshed;
                if (ReadString(record, "device_id").Length == 0)
                    return new LicenseResult(LicenseStatus.ActivationDenied, Detail: writeError);
            }

            return Check(key, record, ignoreDevice: false);
        }
        catch (FirebaseException ex) when (ex.IsPermissionDenied)
        {
            return new LicenseResult(LicenseStatus.AccessDenied, Detail: ex.ShortDescription);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FirebaseException
                                       or JsonException)
        {
            if (ct.IsCancellationRequested)
                throw;
            return new LicenseResult(LicenseStatus.NetworkError, Detail: DescribeError(ex));
        }
    }

    private static LicenseResult Check(string key, JsonElement record, bool ignoreDevice)
    {
        if (!ReadBool(record, "subscription_active"))
            return new LicenseResult(LicenseStatus.Disabled);

        if (!SubscriptionPlan.TryParse(ReadString(record, "subscription_type"), out var plan))
            return new LicenseResult(LicenseStatus.InvalidType);

        if (ignoreDevice)
            return new LicenseResult(LicenseStatus.Valid);

        if (!string.Equals(ReadString(record, "device_id"), DeviceInfo.DeviceId, StringComparison.OrdinalIgnoreCase))
            return new LicenseResult(LicenseStatus.OtherDevice);

        DateTime start = ReadStartUtc(record) ?? ServerClock.UtcNow;
        var license = new LicenseInfo(key, plan, start, ReadString(record, "user_name"));

        return license.IsExpired(ServerClock.UtcNow)
            ? new LicenseResult(LicenseStatus.Expired)
            : new LicenseResult(LicenseStatus.Valid, license);
    }

    /// <summary>
    /// تاريخ البداية: activated_at (وقت الخادم بالمللي ثانية) إن وُجد، وإلا subscription_date (بتوقيت UTC).
    /// </summary>
    internal static DateTime? ReadStartUtc(JsonElement record)
    {
        if (record.TryGetProperty("activated_at", out var activatedAt)
            && activatedAt.ValueKind == JsonValueKind.Number
            && activatedAt.TryGetInt64(out long ms) && ms > 0)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }

        string date = ReadString(record, "subscription_date");
        if (DateTime.TryParseExact(date, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    /// <summary>
    /// يحفظ بيانات التفعيل (الجهاز، اسم المستخدم، تاريخ البداية).
    /// يحاول أولاً مع activated_at (وقت الخادم الفعلي)، وإذا رفضته قواعد قاعدة البيانات
    /// يحفظ بدونه ويُعتمد subscription_date. يرجع null عند النجاح، أو وصف الخطأ.
    /// </summary>
    private static async Task<string?> TryWriteActivationAsync(string path, CancellationToken ct)
    {
        var fields = new Dictionary<string, object>
        {
            ["device_id"] = DeviceInfo.DeviceId,
            ["user_name"] = DeviceInfo.WindowsUserName,
            ["subscription_date"] = ServerClock.UtcNow.ToString(DateFormat, CultureInfo.InvariantCulture),
            // وقت الخادم الفعلي لحظة التفعيل (لا يمكن تزويره من الجهاز)
            ["activated_at"] = new Dictionary<string, string> { [".sv"] = "timestamp" },
        };

        try
        {
            await FirebaseClient.PatchAsync(path, fields, ct).ConfigureAwait(false);
            return null;
        }
        catch (FirebaseException ex) when (ex.IsPermissionDenied)
        {
            // القواعد لا تسمح بحقل activated_at - نحاول بدونه
        }

        fields.Remove("activated_at");
        try
        {
            await FirebaseClient.PatchAsync(path, fields, ct).ConfigureAwait(false);
            return null;
        }
        catch (FirebaseException ex) when (ex.IsPermissionDenied)
        {
            // قد يكون جهاز آخر فعّله في نفس اللحظة - المتصل سيعيد القراءة للتأكد
            return $"رفضت القواعد الحفظ، {ex.ShortDescription}";
        }
    }

    /// <summary>وصف مختصر للخطأ يظهر للمستخدم للمساعدة في التشخيص.</summary>
    public static string DescribeError(Exception ex) => ex switch
    {
        FirebaseException fe => fe.ShortDescription,
        TaskCanceledException => "انتهت مهلة الاتصال",
        HttpRequestException { InnerException: { } inner } => inner.Message,
        _ => ex.Message,
    };

    internal static string ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";

    internal static bool ReadBool(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => string.Equals(value.GetString()?.Trim(), "true", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => value.TryGetInt32(out int n) && n == 1,
            _ => false,
        };
    }

    // مفاتيح Realtime Database لا تقبل . $ # [ ] / لذلك نسمح بالأحرف والأرقام و - _ فقط
    [GeneratedRegex(@"^[A-Za-z0-9_\-]{1,64}$")]
    private static partial Regex KeyPattern();
}
