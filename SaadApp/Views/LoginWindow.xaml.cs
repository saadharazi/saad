using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SaadApp.Services;

namespace SaadApp.Views;

/// <summary>
/// خيارات فتح شاشة الدخول.
/// </summary>
/// <param name="ErrorMessage">رسالة تظهر للمستخدم (مثلاً سبب إخراجه من التطبيق).</param>
/// <param name="RunStartupChecks">فحص التحديث والدخول التلقائي بالكود المحفوظ (عند تشغيل التطبيق فقط).</param>
public sealed record LoginOptions(string? ErrorMessage = null, bool RunStartupChecks = false);

/// <summary>
/// شاشة الدخول بكود التفعيل.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginOptions _options;
    private bool _busy;

    // يُستخدم عند تشغيل التطبيق (StartupUri)
    public LoginWindow() : this(new LoginOptions(RunStartupChecks: true))
    {
    }

    public LoginWindow(LoginOptions options)
    {
        InitializeComponent();
        _options = options;
        VersionText.Text = "v" + AppConfig.CurrentVersion.ToString(CultureInfo.InvariantCulture);

        string? savedKey = RememberedKeyStore.Load();
        if (savedKey is not null)
            AccessKeyTextBox.Text = savedKey;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_options.ErrorMessage is { } message)
            ShowError(message);

        if (_options.RunStartupChecks)
            await RunStartupChecksAsync();
        else
            SetServerStatus(ServerClock.IsSynced);
    }

    /// <summary>
    /// عند تشغيل التطبيق: فحص التحديث أولاً، ثم الدخول تلقائياً إذا كان الكود محفوظاً.
    /// </summary>
    private async Task RunStartupChecksAsync()
    {
        SetBusy(true, "جاري الاتصال...");

        UpdateInfo? update;
        try
        {
            update = await UpdateService.CheckAsync();
            SetServerStatus(true);
        }
        catch (Exception ex)
        {
            SetServerStatus(false);
            SetBusy(false);
            ShowError(ex is FirebaseException { IsPermissionDenied: true } fe
                ? $"قاعدة البيانات رفضت الاتصال، تحقق من قواعد الحماية (Rules) ({fe.ShortDescription})"
                : $"تعذّر الاتصال بالخادم ({LicenseService.DescribeError(ex)})");
            return;
        }

        if (update is not null)
        {
            new UpdateWindow(update).Show();
            Close();
            return;
        }

        SetBusy(false);

        if (RememberedKeyStore.Load() is { } savedKey)
            await LoginAsync(savedKey, fromSavedKey: true);
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        string key = LicenseService.NormalizeKey(AccessKeyTextBox.Text);
        if (key.Length == 0)
        {
            ShowError("أدخل كود التفعيل");
            AccessKeyTextBox.Focus();
            return;
        }

        await LoginAsync(key, fromSavedKey: false);
    }

    private async Task LoginAsync(string key, bool fromSavedKey)
    {
        if (_busy)
            return;

        HideError();
        SetBusy(true, "جاري التحقق...");

        LicenseResult result;
        try
        {
            result = await LicenseService.ActivateAsync(key);
        }
        finally
        {
            SetBusy(false);
        }

        SetServerStatus(!result.IsNetworkError);

        if (!result.IsValid)
        {
            // الكود المحفوظ لم يعد صالحاً (وليس مجرد انقطاع إنترنت) -> نحذفه
            if (fromSavedKey && !result.IsNetworkError)
                RememberedKeyStore.Clear();

            ShowError(result.Message);
            return;
        }

        if (RememberKeyCheckBox.IsChecked == true)
            RememberedKeyStore.Save(key);
        else
            RememberedKeyStore.Clear();

        new MainWindow(result.License!).Show();
        Close();
    }

    /// <summary>
    /// الكود العربي يُعرض من اليمين لليسار بخط عادي، والإنجليزي من اليسار لليمين بخط ثابت العرض.
    /// </summary>
    private void AccessKeyTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        bool arabic = AccessKeyTextBox.Text.Any(c => c is >= '\u0600' and <= '\u06FF');
        var direction = arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        if (AccessKeyTextBox.FlowDirection != direction)
        {
            AccessKeyTextBox.FlowDirection = direction;
            AccessKeyTextBox.FontFamily = (FontFamily)FindResource(arabic ? "MainFont" : "MonoFont");
        }
    }

    private void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                AccessKeyTextBox.Text = LicenseService.NormalizeKey(Clipboard.GetText());
                AccessKeyTextBox.CaretIndex = AccessKeyTextBox.Text.Length;
                AccessKeyTextBox.Focus();
            }
        }
        catch
        {
            // الحافظة مستخدمة من برنامج آخر
        }
    }

    private void StoreButton_Click(object sender, RoutedEventArgs e) => UrlLauncher.Open(AppConfig.StoreUrl);

    private void SupportButton_Click(object sender, RoutedEventArgs e) => UrlLauncher.Open(AppConfig.SupportUrl);

    private void SetBusy(bool busy, string text = "دخول")
    {
        _busy = busy;
        LoginButton.IsEnabled = !busy;
        AccessKeyTextBox.IsEnabled = !busy;
        PasteButton.IsEnabled = !busy;
        LoginButtonText.Text = busy ? text : "دخول";
        LoginButtonArrow.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetServerStatus(bool online)
    {
        var color = online ? Color.FromRgb(0x2E, 0xCC, 0x71) : Color.FromRgb(0xE0, 0x30, 0x4F);
        ServerStatusDot.Fill = new SolidColorBrush(color);
        ServerStatusDot.Effect = new DropShadowEffect { Color = color, BlurRadius = 8, ShadowDepth = 0, Opacity = 1 };
        ServerStatusPill.Background = new SolidColorBrush(Color.FromArgb(0x14, color.R, color.G, color.B));
        ServerStatusPill.BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, color.R, color.G, color.B));
        ServerStatusText.Foreground = new SolidColorBrush(online
            ? Color.FromRgb(0x7F, 0xD8, 0xA0)
            : Color.FromRgb(0xFF, 0x6B, 0x8B));
        ServerStatusText.Text = online ? "الخادم متصل" : "الخادم غير متصل";
    }

    private void ShowError(string message)
    {
        ErrorMessageText.Text = message;
        ErrorMessageBorder.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorMessageBorder.Visibility = Visibility.Collapsed;

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
