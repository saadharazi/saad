using System.Globalization;
using System.Text.RegularExpressions;

namespace SaadApp.Services;

/// <summary>
/// نوع الاشتراك المقروء من الحقل subscription_type:
/// "lifetime" = مدى الحياة، "1day" = يوم واحد، "30" أو "30day" أو "30days" = 30 يوم ... إلخ.
/// </summary>
public sealed partial record SubscriptionPlan(bool IsLifetime, int Days)
{
    public TimeSpan Duration => TimeSpan.FromDays(Days);

    public string DisplayName => IsLifetime ? "LIFETIME" : Days == 1 ? "1 DAY" : $"{Days} DAYS";

    public static bool TryParse(string? value, out SubscriptionPlan plan)
    {
        plan = new SubscriptionPlan(false, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string text = value.Trim().ToLowerInvariant();
        if (text.StartsWith('-'))
            return false;

        text = text.Replace(" ", "").Replace("_", "").Replace("-", "");

        if (text is "lifetime" or "life" or "forever" or "مدىالحياة" or "مدىالحياه")
        {
            plan = new SubscriptionPlan(true, 0);
            return true;
        }

        var match = DaysPattern().Match(text);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int days)
            || days <= 0)
            return false;

        plan = new SubscriptionPlan(false, days);
        return true;
    }

    [GeneratedRegex(@"^(\d{1,5})(d|day|days|يوم|ايام|أيام)?$")]
    private static partial Regex DaysPattern();
}
