using System.Diagnostics;

namespace SaadApp.Services;

/// <summary>
/// وقت الخادم. يُضبط من ترويسة Date في ردود الخادم ثم يتقدم بساعة داخلية (Stopwatch)،
/// فتغيير ساعة ويندوز لا يؤثر على حساب انتهاء الاشتراك.
/// </summary>
public static class ServerClock
{
    private static readonly object Sync = new();
    private static readonly Stopwatch Elapsed = new();
    private static DateTime _serverUtcAtSync;
    private static bool _synced;

    public static bool IsSynced
    {
        get { lock (Sync) return _synced; }
    }

    public static DateTime UtcNow
    {
        get
        {
            lock (Sync)
                return _synced ? _serverUtcAtSync + Elapsed.Elapsed : DateTime.UtcNow;
        }
    }

    public static void Update(DateTimeOffset? serverDate)
    {
        if (serverDate is null)
            return;

        lock (Sync)
        {
            _serverUtcAtSync = serverDate.Value.UtcDateTime;
            Elapsed.Restart();
            _synced = true;
        }
    }
}
