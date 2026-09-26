using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private bool _checkingUpdate;

    public MainWindow(LicenseInfo license)
    {
        InitializeComponent();

        _settings = AimSettings.Load();
        ApplySettings(_settings);
        InitializeSettingsPage();
        InitializeContactPage();

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
        AccountKeyText.Text = KeyText.Text;
        AccountKeyText.FlowDirection = KeyText.FlowDirection;
        AccountPlanText.Text = license.Plan.DisplayName;
        AccountExpiryText.Text = license.ExpiresUtc is { } expires ? FormatDateTime(expires) : "لا ينتهي";

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

    // التاريخ داخل علامتي اتجاه (LRE ... PDF) حتى يظهر بترتيبه الصحيح داخل النص العربي
    private static string FormatDate(DateTime utc) =>
        "\u202A" + utc.ToLocalTime().ToString("yyyy/MM/dd", DateCulture) + "\u202C";

    private static string FormatDateTime(DateTime utc) =>
        "\u202A" + utc.ToLocalTime().ToString("yyyy/MM/dd hh:mm tt", DateCulture) + "\u202C";

    private static bool ContainsArabic(string text) => text.Any(c => c is >= '؀' and <= 'ۿ');

    private static string MaskKey(string key) =>
        key.Length <= 4 ? key : new string('•', Math.Min(key.Length - 4, 8)) + key[^4..];

    /// <summary>إخراج المستخدم إلى شاشة الدخول مع سبب الإخراج (null = تسجيل خروج عادي).</summary>
    private void Kick(string? reason, bool forgetKey)
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
        _settings.AlwaysOnTop = Topmost;
        _settings.Target = TargetChestRadio.IsChecked == true ? "Chest"
            : TargetBodyRadio.IsChecked == true ? "Body"
            : "Head";
        _settings.Save();
    }

    // ------------------------------------------------------------------
    // التنقل بين الصفحات
    // ------------------------------------------------------------------

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        // يُستدعى أيضاً أثناء تحميل الواجهة، والصفحة الرئيسية ظاهرة افتراضياً
        if (!IsLoaded)
            return;

        FrameworkElement page = sender == NavSettings ? SettingsPage
            : sender == NavContact ? ContactPage
            : HomePage;
        ShowPage(page);
    }

    private void ShowPage(FrameworkElement page)
    {
        foreach (var p in new FrameworkElement[] { HomePage, SettingsPage, ContactPage })
            p.Visibility = p == page ? Visibility.Visible : Visibility.Collapsed;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
        if (page.RenderTransform is TranslateTransform move)
            move.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
    }

    // ------------------------------------------------------------------
    // صفحة الإعدادات
    // ------------------------------------------------------------------

    private void InitializeSettingsPage()
    {
        string version = "v" + AppConfig.CurrentVersion.ToString(CultureInfo.InvariantCulture);
        SettingsVersionText.Text = version;
        SidebarVersionText.Text = version;

        Topmost = _settings.AlwaysOnTop;
        AlwaysOnTopToggle.IsChecked = _settings.AlwaysOnTop;
        StartWithWindowsToggle.IsChecked = StartupRegistration.IsEnabled;
        RememberKeyToggle.IsChecked = RememberedKeyStore.Load() is not null;

        WindowsUserText.Text = DeviceInfo.WindowsUserName;
        DeviceIdText.Text = DeviceInfo.DeviceId[..12] + "…";
    }

    private void StartWithWindowsToggle_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = StartWithWindowsToggle.IsChecked == true;
        if (!StartupRegistration.Set(enabled))
            StartWithWindowsToggle.IsChecked = !enabled;
    }

    private void AlwaysOnTopToggle_Click(object sender, RoutedEventArgs e)
    {
        Topmost = AlwaysOnTopToggle.IsChecked == true;
        SaveSettings();
    }

    private void RememberKeyToggle_Click(object sender, RoutedEventArgs e)
    {
        if (RememberKeyToggle.IsChecked == true)
            RememberedKeyStore.Save(_monitor.License.Key);
        else
            RememberedKeyStore.Clear();
    }

    private void ResetAimSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySettings(new AimSettings());
        SaveSettings();
        NavHome.IsChecked = true;
    }

    private void CopyDeviceIdButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryCopy(DeviceInfo.DeviceId))
            FlashText(CopyDeviceIdButton, "تم ✓", "نسخ");
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        RememberedKeyStore.Clear();
        Kick(null, forgetKey: true);
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_checkingUpdate)
            return;
        _checkingUpdate = true;

        NavSettings.IsChecked = true;
        CheckUpdateButton.IsEnabled = false;
        SidebarUpdateButton.IsEnabled = false;
        CheckUpdateButtonText.Text = "جاري التحقق...";
        UpdateStatusBorder.Visibility = Visibility.Collapsed;

        try
        {
            UpdateInfo? update = await UpdateService.CheckAsync();
            if (update is not null)
            {
                // تحديث إجباري: شاشة التحديث تحل محل التطبيق
                _kicked = true;
                new UpdateWindow(update).Show();
                Close();
                return;
            }

            ShowUpdateStatus(true, $"أنت تستخدم آخر إصدار ({SettingsVersionText.Text})");
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(false, "تعذّر التحقق من التحديثات: " + LicenseService.DescribeError(ex));
        }
        finally
        {
            _checkingUpdate = false;
            CheckUpdateButton.IsEnabled = true;
            SidebarUpdateButton.IsEnabled = true;
            CheckUpdateButtonText.Text = "التحقق من التحديثات";
        }
    }

    private void ShowUpdateStatus(bool success, string message)
    {
        var color = success ? Color.FromRgb(0x2E, 0xCC, 0x71) : Color.FromRgb(0xE0, 0x30, 0x4F);
        var textColor = new SolidColorBrush(success ? Color.FromRgb(0x7F, 0xD8, 0xA0) : Color.FromRgb(0xFF, 0x6B, 0x8B));
        UpdateStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x14, color.R, color.G, color.B));
        UpdateStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, color.R, color.G, color.B));
        UpdateStatusIcon.Text = success ? "\uE73E" : "\uE783";
        UpdateStatusIcon.Foreground = textColor;
        UpdateStatusText.Foreground = textColor;
        UpdateStatusText.Text = message;
        UpdateStatusBorder.Visibility = Visibility.Visible;
    }

    private void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        _kicked = true;
        _monitor.Stop();
        AppRestarter.Restart();
    }

    // ------------------------------------------------------------------
    // صفحة تواصل معنا
    // ------------------------------------------------------------------

    private void InitializeContactPage()
    {
        SetupContactButton(DiscordButton, AppConfig.DiscordUrl);
        SetupContactButton(WhatsAppButton, AppConfig.WhatsAppUrl);
        SetupContactButton(TelegramButton, AppConfig.TelegramUrl);
        SetupContactButton(StoreButton, AppConfig.StoreUrl);
    }

    private static void SetupContactButton(System.Windows.Controls.Button button, string url)
    {
        bool available = !string.IsNullOrWhiteSpace(url);
        button.IsEnabled = available;
        button.Content = available ? "فتح" : "قريباً";
    }

    private void ContactButton_Click(object sender, RoutedEventArgs e)
    {
        string url = (sender as FrameworkElement)?.Tag switch
        {
            "Discord" => AppConfig.DiscordUrl,
            "WhatsApp" => AppConfig.WhatsAppUrl,
            "Telegram" => AppConfig.TelegramUrl,
            "Store" => AppConfig.StoreUrl,
            _ => "",
        };
        UrlLauncher.Open(url);
    }

    private void CopySupportInfoButton_Click(object sender, RoutedEventArgs e)
    {
        LicenseInfo license = _monitor.License;
        string info = string.Join(Environment.NewLine,
            $"الكود: {license.Key}",
            $"نوع الاشتراك: {license.Plan.DisplayName}",
            $"ينتهي: {(license.ExpiresUtc is { } expires ? expires.ToLocalTime().ToString("yyyy/MM/dd hh:mm tt", DateCulture) : "لا ينتهي")}",
            $"معرّف الجهاز: {DeviceInfo.DeviceId}",
            $"مستخدم ويندوز: {DeviceInfo.WindowsUserName}",
            $"الإصدار: v{AppConfig.CurrentVersion.ToString(CultureInfo.InvariantCulture)}");

        if (TryCopy(info))
            FlashText(CopySupportInfoText, "تم النسخ ✓", "نسخ بيانات الدعم");
    }

    private static bool TryCopy(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch
        {
            // الحافظة مستخدمة من برنامج آخر
            return false;
        }
    }

    /// <summary>يغيّر نص الزر مؤقتاً (مثل "تم النسخ ✓") ثم يعيده.</summary>
    private static void FlashText(object target, string temporary, string original)
    {
        void Set(string text)
        {
            if (target is System.Windows.Controls.TextBlock block)
                block.Text = text;
            else if (target is System.Windows.Controls.ContentControl control)
                control.Content = text;
        }

        Set(temporary);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Set(original);
        };
        timer.Start();
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
