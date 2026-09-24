using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SaadApp.Services;

namespace SaadApp.Views;

/// <summary>
/// الشاشة الرئيسية. تعرض بيانات الاشتراك وتُخرج المستخدم فوراً عند انتهائه أو إلغائه.
/// </summary>
public partial class MainWindow : Window
{
    private readonly LicenseMonitor _monitor;
    private readonly DispatcherTimer _uiTimer;
    private bool _kicked;

    public MainWindow(LicenseInfo license)
    {
        InitializeComponent();

        _monitor = new LicenseMonitor(license);
        _monitor.Revoked += Kick;
        _monitor.LicenseChanged += _ => UpdateSubscriptionUi();

        _uiTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(500) };
        _uiTimer.Tick += (_, _) => UpdateSubscriptionUi();

        UpdateSubscriptionUi();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _monitor.Start();
        _uiTimer.Start();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _uiTimer.Stop();
        _monitor.Dispose();
    }

    private void UpdateSubscriptionUi()
    {
        LicenseInfo license = _monitor.License;
        DateTime now = ServerClock.UtcNow;

        KeyText.Text = MaskKey(license.Key);
        PlanText.Text = license.Plan.DisplayName;

        if (license.Plan.IsLifetime)
        {
            DaysLeftText.Text = "∞";
            DaysUnitText.Text = "مدى الحياة";
            RemainingBar.Value = 100;
            ExpiryText.Text = $"اشتراك دائم · بدأ {license.StartUtc.ToLocalTime():yyyy/MM/dd}";
            return;
        }

        TimeSpan remaining = license.Remaining(now) ?? TimeSpan.Zero;
        DaysLeftText.Text = ((int)remaining.TotalDays).ToString();
        DaysUnitText.Text = "يوم";
        RemainingBar.Value = Math.Clamp(remaining.TotalSeconds / license.Plan.Duration.TotalSeconds * 100, 0, 100);
        ExpiryText.Text =
            $"متبقي {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00} · ينتهي {license.ExpiresUtc!.Value.ToLocalTime():yyyy/MM/dd HH:mm}";

        // احتياط إضافي: المراقب يُخرج المستخدم، وهنا نتأكد أيضاً من الواجهة
        if (license.IsExpired(now))
            Kick("انتهى اشتراكك", true);
    }

    private static string MaskKey(string key) =>
        key.Length <= 4 ? key : new string('•', Math.Min(key.Length - 4, 8)) + key[^4..];

    /// <summary>إخراج المستخدم إلى شاشة الدخول مع سبب الإخراج.</summary>
    private void Kick(string reason, bool forgetKey)
    {
        if (_kicked)
            return;
        _kicked = true;

        _uiTimer.Stop();
        _monitor.Stop();

        if (forgetKey)
            RememberedKeyStore.Clear();

        new LoginWindow(new LoginOptions(ErrorMessage: reason)).Show();
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
