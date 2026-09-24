namespace SaadApp.Services;

/// <summary>
/// إعدادات الاتصال والتطبيق. كل ما تحتاج تغييره موجود هنا.
/// </summary>
public static class AppConfig
{
    /// <summary>
    /// رابط Realtime Database (من صفحة Realtime Database في Firebase، أعلى شجرة البيانات).
    /// لا يُوضع هنا أي مفتاح خاص (private key) أبداً - الحماية تتم عبر قواعد قاعدة البيانات.
    /// </summary>
    public const string DatabaseUrl = "https://boost-72544-default-rtdb.firebaseio.com";

    /// <summary>
    /// Web API Key (من إعدادات المشروع في Firebase). يُستخدم لتسجيل دخول مجهول (Anonymous)
    /// حتى تعمل قواعد قاعدة البيانات التي تشترط auth != null. هذا المفتاح عام وليس سرياً.
    /// </summary>
    public const string ApiKey = "AIzaSyB7XXgcsV_GM9dG61_NINDHLL1Jod7c578";

    /// <summary>
    /// رقم إصدار هذه النسخة. عند رفع نسخة جديدة: زِد هذا الرقم، ثم حدّث "Update number" في قاعدة البيانات.
    /// </summary>
    public const double CurrentVersion = 20.2;

    /// <summary>رابط المتجر (زر "شراء كود").</summary>
    public const string StoreUrl = "";

    /// <summary>رابط الدعم الفني (زر "الدعم الفني").</summary>
    public const string SupportUrl = "";

    /// <summary>المدة القصوى المسموح بها بدون اتصال بالخادم قبل إخراج المستخدم.</summary>
    public static readonly TimeSpan MaxOfflineDuration = TimeSpan.FromMinutes(3);
}
