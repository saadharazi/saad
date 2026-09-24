using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace SaadApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // التواريخ والأرقام بالميلادي والإنجليزي دائماً، حتى لو كانت لغة ويندوز عربية (التقويم الهجري)
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        base.OnStartup(e);
    }
}
