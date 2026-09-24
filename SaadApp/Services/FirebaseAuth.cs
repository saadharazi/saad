using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SaadApp.Services;

/// <summary>
/// تسجيل دخول مجهول (Anonymous) في Firebase Authentication باستخدام Web API Key.
/// الرمز (ID token) يُرفق مع طلبات قاعدة البيانات ويُجدَّد تلقائياً قبل انتهائه.
/// إذا فشل تسجيل الدخول (مثلاً Anonymous غير مفعّل) تستمر الطلبات بدون رمز.
/// </summary>
public static class FirebaseAuth
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static readonly string RefreshTokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BoostStore", "auth.dat");

    private static string? _idToken;
    private static string? _refreshToken;
    private static DateTime _expiresUtc;

    /// <summary>آخر خطأ في تسجيل الدخول (للتشخيص).</summary>
    public static string? LastError { get; private set; }

    /// <summary>يرجع رمزاً صالحاً، أو null إذا تعذّر تسجيل الدخول.</summary>
    public static async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(AppConfig.ApiKey))
            return null;

        if (_idToken is not null && DateTime.UtcNow < _expiresUtc)
            return _idToken;

        await Lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_idToken is not null && DateTime.UtcNow < _expiresUtc)
                return _idToken;

            _refreshToken ??= LoadRefreshToken();

            // نجدد الجلسة السابقة بدل إنشاء مستخدم مجهول جديد في كل مرة
            if (_refreshToken is not null && await TryRefreshAsync(ct).ConfigureAwait(false))
                return _idToken;

            if (await TrySignUpAsync(ct).ConfigureAwait(false))
                return _idToken;

            return null;
        }
        finally
        {
            Lock.Release();
        }
    }

    /// <summary>يُلغي الرمز الحالي (عند رفض الخادم له) ليُطلب رمز جديد.</summary>
    public static void Invalidate() => _idToken = null;

    private static async Task<bool> TrySignUpAsync(CancellationToken ct)
    {
        try
        {
            using var content = new StringContent("""{"returnSecureToken":true}""", Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={AppConfig.ApiKey}", content, ct)
                .ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"signUp {(int)response.StatusCode}: {ReadErrorMessage(json)}";
                return false;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Store(root.GetProperty("idToken").GetString(), root.GetProperty("refreshToken").GetString(),
                root.GetProperty("expiresIn").GetString());
            LastError = null;
            return _idToken is not null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException)
        {
            if (ct.IsCancellationRequested)
                throw;
            LastError = "signUp: " + ex.Message;
            return false;
        }
    }

    private static async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = _refreshToken!,
            });
            using var response = await Http.PostAsync(
                $"https://securetoken.googleapis.com/v1/token?key={AppConfig.ApiKey}", content, ct)
                .ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _refreshToken = null;
                return false;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Store(root.GetProperty("id_token").GetString(), root.GetProperty("refresh_token").GetString(),
                root.GetProperty("expires_in").GetString());
            return _idToken is not null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException)
        {
            if (ct.IsCancellationRequested)
                throw;
            return false;
        }
    }

    private static void Store(string? idToken, string? refreshToken, string? expiresIn)
    {
        int seconds = int.TryParse(expiresIn, out int s) ? s : 3600;
        _idToken = idToken;
        _refreshToken = refreshToken;
        // نجدد قبل الانتهاء بخمس دقائق
        _expiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, seconds - 300));
        SaveRefreshToken(refreshToken);
    }

    private static string ReadErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? json;
        }
        catch
        {
            return json;
        }
    }

    private static string? LoadRefreshToken()
    {
        try
        {
            if (!File.Exists(RefreshTokenPath))
                return null;
            byte[] data = ProtectedData.Unprotect(File.ReadAllBytes(RefreshTokenPath), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveRefreshToken(string? token)
    {
        if (token is null)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RefreshTokenPath)!);
            byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(RefreshTokenPath, data);
        }
        catch
        {
            // الحفظ اختياري
        }
    }
}
