using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SaadApp.Services;

/// <summary>
/// اتصال بسيط بـ Firebase Realtime Database عبر REST.
/// كل رد من الخادم يُستخدم أيضاً لضبط <see cref="ServerClock"/>.
/// </summary>
public static class FirebaseClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // اتصال البث المباشر يبقى مفتوحاً، فلا نحدد له مهلة
    private static readonly HttpClient StreamHttp = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static string BuildUrl(string path) => $"{AppConfig.DatabaseUrl.TrimEnd('/')}/{path}.json";

    /// <summary>يقرأ عقدة. يرجع null إذا كانت غير موجودة.</summary>
    public static async Task<JsonElement?> GetAsync(string path, CancellationToken ct = default)
    {
        string json = await SendWithAuthAsync(
            url => new HttpRequestMessage(HttpMethod.Get, url), path, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Null ? null : doc.RootElement.Clone();
    }

    /// <summary>يحدّث حقولاً داخل عقدة (PATCH).</summary>
    public static async Task PatchAsync(string path, object body, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(body);
        await SendWithAuthAsync(url => new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }, path, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// يرسل الطلب مع رمز تسجيل الدخول. إذا رُفض الرمز يُطلب رمز جديد ويعاد الطلب مرة واحدة.
    /// </summary>
    private static async Task<string> SendWithAuthAsync(
        Func<string, HttpRequestMessage> createRequest, string path, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            string? token = await FirebaseAuth.GetTokenAsync(ct).ConfigureAwait(false);
            using var request = createRequest(BuildAuthUrl(path, token));
            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            ServerClock.Update(response.Headers.Date);

            if (token is not null && attempt == 0 && response.StatusCode == HttpStatusCode.Unauthorized)
            {
                FirebaseAuth.Invalidate();
                continue;
            }

            await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
    }

    private static string BuildAuthUrl(string path, string? token) =>
        token is null ? BuildUrl(path) : $"{BuildUrl(path)}?auth={Uri.EscapeDataString(token)}";

    /// <summary>
    /// يستمع لأي تغيير على العقدة (Server-Sent Events) ويستدعي onChange عند كل تغيير.
    /// يرجع عند انقطاع الاتصال أو الإلغاء.
    /// </summary>
    public static async Task ListenAsync(string path, Func<Task> onChange, CancellationToken ct)
    {
        string? token = await FirebaseAuth.GetTokenAsync(ct).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildAuthUrl(path, token));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await StreamHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        ServerClock.Update(response.Headers.Date);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!ct.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
                return; // أغلق الخادم الاتصال

            if (!line.StartsWith("event:", StringComparison.Ordinal))
                continue;

            string eventName = line["event:".Length..].Trim();
            if (eventName is "put" or "patch" or "cancel" or "auth_revoked")
                await onChange().ConfigureAwait(false);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        string body = "";
        try
        {
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // تجاهل
        }

        throw new FirebaseException(response.StatusCode, body);
    }
}

public sealed class FirebaseException(HttpStatusCode statusCode, string body)
    : Exception($"Firebase request failed: {(int)statusCode} {body}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string ShortDescription => $"HTTP {(int)StatusCode}";

    public bool IsPermissionDenied =>
        StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
