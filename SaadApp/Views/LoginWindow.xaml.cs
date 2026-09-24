using System.Windows;
using System.Windows.Input;

namespace SaadApp.Views;

/// <summary>
/// شاشة تسجيل الدخول - تصميم فقط، بدون منطق تسجيل الدخول حالياً.
/// </summary>
public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        // حالياً للتصميم فقط: ننتقل للشاشة الرئيسية بدون التحقق من الكود
        new MainWindow().Show();
        Close();
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
