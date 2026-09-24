using System.Diagnostics;

namespace SaadApp.Services;

/// <summary>
/// يراقب الاشتراك أثناء تشغيل التطبيق:
/// - يُخرج المستخدم في نفس الثانية التي ينتهي فيها الاشتراك (حسب وقت الخادم، لا ساعة الجهاز).
/// - يستمع لأي تغيير على الكود في قاعدة البيانات (إيقاف، حذف، تغيير الجهاز) ويطبقه فوراً.
/// - يعيد التحقق دورياً، ويُخرج المستخدم إذا انقطع الاتصال بالخادم أكثر من المدة المسموحة.
/// الأحداث تُستدعى على نفس الـ Thread الذي أُنشئ فيه المراقب (واجهة المستخدم).
/// </summary>
public sealed class LicenseMonitor : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly SynchronizationContext _context;
    private readonly CancellationTokenSource _cts = new();
    private readonly Stopwatch _sinceLastContact = Stopwatch.StartNew();
    private readonly SemaphoreSlim _validateLock = new(1, 1);
    private Timer? _expiryTimer;
    private int _revoked;

    public LicenseMonitor(LicenseInfo license)
    {
        License = license;
        _context = SynchronizationContext.Current ?? new SynchronizationContext();
    }

    public LicenseInfo License { get; private set; }

    /// <summary>انتهى الاشتراك أو أُلغي. الوسيط = سبب الإخراج. شطب الكود المحفوظ = true.</summary>
    public event Action<string, bool>? Revoked;

    /// <summary>تغيّرت بيانات الاشتراك (مثلاً تمديد المدة من قاعدة البيانات).</summary>
    public event Action<LicenseInfo>? LicenseChanged;

    public void Start()
    {
        _expiryTimer = new Timer(_ => CheckExpiry(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(250));
        _ = Task.Run(() => ListenLoopAsync(_cts.Token));
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    private void CheckExpiry()
    {
        if (License.IsExpired(ServerClock.UtcNow))
            Revoke("انتهى اشتراكك", forgetKey: true);
        else if (_sinceLastContact.Elapsed > AppConfig.MaxOfflineDuration)
            Revoke("انقطع الاتصال بالخادم", forgetKey: false);
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await FirebaseClient.ListenAsync($"users/{License.Key}", () => ValidateAsync(ct), ct)
                    .ConfigureAwait(false);
            }
            catch when (!ct.IsCancellationRequested)
            {
                // سيعاد الاتصال
            }
            catch
            {
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
            }
            catch
            {
                return;
            }
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
                await ValidateAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                if (ct.IsCancellationRequested)
                    return;
            }
        }
    }

    private async Task ValidateAsync(CancellationToken ct)
    {
        await _validateLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var result = await LicenseService.ValidateAsync(License.Key, ct).ConfigureAwait(false);
            if (result.IsNetworkError)
                return;

            _sinceLastContact.Restart();

            if (!result.IsValid)
            {
                Revoke(result.Message, forgetKey: true);
                return;
            }

            if (result.License is { } updated && updated != License)
            {
                License = updated;
                _context.Post(_ => LicenseChanged?.Invoke(updated), null);
            }
        }
        finally
        {
            _validateLock.Release();
        }
    }

    private void Revoke(string reason, bool forgetKey)
    {
        if (Interlocked.Exchange(ref _revoked, 1) == 1)
            return;

        Stop();
        _context.Post(_ => Revoked?.Invoke(reason, forgetKey), null);
    }

    public void Stop()
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }
}
