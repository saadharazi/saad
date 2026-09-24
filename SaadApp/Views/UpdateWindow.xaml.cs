using System.Globalization;
using System.Windows;
using System.Windows.Input;
using SaadApp.Services;

namespace SaadApp.Views;

/// <summary>
/// شاشة التحديث: تظهر عند وجود نسخة أحدث في قاعدة البيانات، ولا يمكن المتابعة بدون التحديث.
/// </summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _update;

    public UpdateWindow(UpdateInfo update)
    {
        InitializeComponent();
        _update = update;
        CurrentVersionText.Text = "v" + AppConfig.CurrentVersion.ToString(CultureInfo.InvariantCulture);
        NewVersionText.Text = "v" + update.Version.ToString(CultureInfo.InvariantCulture);
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        UrlLauncher.Open(_update.Url);
        Application.Current.Shutdown();
    }

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
        Application.Current.Shutdown();
    }
}
