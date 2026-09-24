using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SaadApp.Services;

namespace SaadApp.Views;

/// <summary>
/// الشاشة الرئيسية. تعرض بيانات الاشتراك وتُخرج المستخدم فوراً عند انتهائه أو إلغائه.
/// </summary>
public partial class MainWindow : Window
{
    // التواريخ دائماً ميلادية وبالإنجليزي
    private static readonly CultureInfo DateCulture = CultureInfo.GetCultureInfo("en-US");
    private const int LatencyHistorySize = 17;

    private readonly LicenseMonitor _monitor;
    private readonly DispatcherTimer _uiTimer;
    private readonly DispatcherTimer _statsTimer;
    private readonly PerformanceSampler _performance = new();
    private readonly Queue<int> _latencyHistory = new();
    private readonly AimSettings _settings;
    private bool _kicked;

    public MainWindow(LicenseInfo license)
    {
        InitializeComponent();

        _settings = AimSettings.Load();
        ApplySettings(_settings);

        _monitor = new LicenseMonitor(license);
        _monitor.Revoked += Kick;
        _monitor.LicenseChanged += _ => UpdateSubscriptionUi();

        _uiTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(500) };
        _uiTimer.Tick += (_, _) => UpdateSubscriptionUi();

        _statsTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _statsTimer.Tick += (_, _) => UpdateStats();

        UpdateSubscriptionUi();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _monitor.Start();
        _uiTimer.Start();
        _statsTimer.Start();
        UpdateStats();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _uiTimer.Stop();
        _statsTimer.Stop();
        _monitor.Dispose();
        SaveSettings();
    }

    // ------------------------------------------------------------------
    // الاشتراك
    // ------------------------------------------------------------------

    private void UpdateSubscriptionUi()
    {
        LicenseInfo license = _monitor.License;
        DateTime now = ServerClock.UtcNow;

        KeyText.Text = MaskKey(license.Key);
        KeyText.FlowDirection = ContainsArabic(license.Key) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        PlanText.Text = license.Plan.DisplayName;

        if (license.Plan.IsLifetime)
        {
            DaysLeftText.Text = "∞";
            DaysUnitText.Text = "مدى الحياة";
            RemainingBar.Value = 100;
            ExpiryText.Text = $"اشتراك دائم · بدأ {FormatDate(license.StartUtc)}";
            return;
        }

        TimeSpan remaining = license.Remaining(now) ?? TimeSpan.Zero;
        DaysLeftText.Text = ((int)remaining.TotalDays).ToString(CultureInfo.InvariantCulture);
        DaysUnitText.Text = "يوم";
        RemainingBar.Value = Math.Clamp(remaining.TotalSeconds / license.Plan.Duration.TotalSeconds * 100, 0, 100);
        ExpiryText.Text = string.Format(DateCulture, "متبقي {0:00}:{1:00}:{2:00} · ينتهي {3}",
            remaining.Hours, remaining.Minutes, remaining.Seconds, FormatDateTime(license.ExpiresUtc!.Value));

        // احتياط إضافي: المراقب يُخرج المستخدم، وهنا نتأكد أيضاً من الواجهة
        if (license.IsExpired(now))
            Kick("انتهى اشتراكك", true);
    }

    private static string FormatDate(DateTime utc) =>
        utc.ToLocalTime().ToString("yyyy/MM/dd", DateCulture);

    private static string FormatDateTime(DateTime utc) =>
        utc.ToLocalTime().ToString("yyyy/MM/dd hh:mm tt", DateCulture);

    private static bool ContainsArabic(string text) => text.Any(c => c is >= '؀' and <= 'ۿ');

    private static string MaskKey(string key) =>
        key.Length <= 4 ? key : new string('•', Math.Min(key.Length - 4, 8)) + key[^4..];

    /// <summary>إخراج المستخدم إلى شاشة الدخول مع سبب الإخراج.</summary>
    private void Kick(string reason, bool forgetKey)
    {
        if (_kicked)
            return;
        _kicked = true;

        _uiTimer.Stop();
        _statsTimer.Stop();
        _monitor.Stop();

        if (forgetKey)
            RememberedKeyStore.Clear();

        new LoginWindow(new LoginOptions(ErrorMessage: reason)).Show();
        Close();
    }

    // ------------------------------------------------------------------
    // الأداء والاتصال
    // ------------------------------------------------------------------

    private void UpdateStats()
    {
        var (cpu, memory) = _performance.Sample();
        CpuText.Text = cpu.ToString(cpu < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + "%";
        RamValue.Text = memory.ToString("0", CultureInfo.InvariantCulture);

        if (FirebaseClient.LastLatencyMs is not { } latency)
            return;

        LatencyValue.Text = latency.ToString(CultureInfo.InvariantCulture);
        ConnectionText.Text = $"متصل · {latency}ms";
        PerformanceLabel.Text = latency switch
        {
            < 150 => "أداء عالي",
            < 400 => "أداء جيد",
            _ => "اتصال بطيء",
        };

        _latencyHistory.Enqueue(latency);
        while (_latencyHistory.Count > LatencyHistorySize)
            _latencyHistory.Dequeue();
        DrawLatencyChart();
    }

    /// <summary>يرسم منحنى زمن الاتصال داخل مساحة 320 × 46.</summary>
    private void DrawLatencyChart()
    {
        int[] values = _latencyHistory.ToArray();
        if (values.Length < 2)
            return;

        double max = Math.Max(values.Max() * 1.25, 50);
        double step = 320.0 / (LatencyHistorySize - 1);
        double startX = 320 - step * (values.Length - 1);

        var line = new PointCollection();
        for (int i = 0; i < values.Length; i++)
            line.Add(new Point(startX + i * step, 42 - values[i] / max * 36));

        var area = new PointCollection(line) { new Point(320, 46), new Point(startX, 46) };
        LatencyLine.Points = line;
        LatencyArea.Points = area;
    }

    // ------------------------------------------------------------------
    // الإعدادات
    // ------------------------------------------------------------------

    private void ApplySettings(AimSettings s)
    {
        AimAssistToggle.IsChecked = s.Enabled;
        StrengthSlider.Value = s.Strength;
        SmoothSlider.Value = s.Smoothness;
        FovSlider.Value = s.Fov;
        ReactionSlider.Value = s.ReactionMs;
        ShowFovToggle.IsChecked = s.ShowFov;
        PredictionToggle.IsChecked = s.Prediction;
        ClosestTargetToggle.IsChecked = s.ClosestTarget;

        (s.Mode switch
        {
            "Smooth" => ModeSmoothRadio,
            "Strong" => ModeStrongRadio,
            _ => ModeBalancedRadio,
        }).IsChecked = true;

        (s.Target switch
        {
            "Chest" => TargetChestRadio,
            "Body" => TargetBodyRadio,
            _ => TargetHeadRadio,
        }).IsChecked = true;
    }

    private void SaveSettings()
    {
        _settings.Enabled = AimAssistToggle.IsChecked == true;
        _settings.Strength = StrengthSlider.Value;
        _settings.Smoothness = SmoothSlider.Value;
        _settings.Fov = FovSlider.Value;
        _settings.ReactionMs = ReactionSlider.Value;
        _settings.ShowFov = ShowFovToggle.IsChecked == true;
        _settings.Prediction = PredictionToggle.IsChecked == true;
        _settings.ClosestTarget = ClosestTargetToggle.IsChecked == true;
        _settings.Mode = ModeSmoothRadio.IsChecked == true ? "Smooth"
            : ModeStrongRadio.IsChecked == true ? "Strong"
            : "Balanced";
        _settings.Target = TargetChestRadio.IsChecked == true ? "Chest"
            : TargetBodyRadio.IsChecked == true ? "Body"
            : "Head";
        _settings.Save();
    }

    // ------------------------------------------------------------------
    // النافذة
    // ------------------------------------------------------------------

    /// <summary>F1 = تشغيل/إيقاف سريع (داخل نافذة التطبيق).</summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            AimAssistToggle.IsChecked = AimAssistToggle.IsChecked != true;
            e.Handled = true;
        }
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
