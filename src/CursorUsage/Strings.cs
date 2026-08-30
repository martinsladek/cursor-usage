using System.Globalization;

namespace CursorUsage;

static class Strings
{
    private static readonly string Lang =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

    public const string ProductName = "Cursor Usage";
    public const string ConfigFolderName = "CursorUsage";

    public const string WebsiteUrl = "https://www.martinsladek.com/";
    public const string GitHubUrl = "https://github.com/martinsladek/cursor-usage";
    public const string SpendingUrl = "https://cursor.com/dashboard/spending#included-in-pro";

    public static string AppName => ProductName;

    public static string Refresh => L(en: "Refresh", cs: "Obnovit");

    public static string OpenCursorSpending => L(en: "Open Cursor spending", cs: "Otevřít Cursor spending");

    public static string StartWithWindows => L(en: "Start with Windows", cs: "Spouštět s Windows");

    public static string About => L(en: "About", cs: "O aplikaci");

    public static string Exit => L(en: "Exit", cs: "Ukončit");

    public static string Ok => "OK";

    public static string IncludedInPro => "Included in Pro";

    public static string AutoPool => L(en: "Cursor Models (Auto)", cs: "Cursor modely (Auto)");

    public static string ApiPool => L(en: "Other Models (API)", cs: "Ostatní modely (API)");

    public static string RecentIncluded => L(en: "Recent included usage", cs: "Nedávné included využití");

    public static string LagNote => L(
        en: "Usage can lag by several minutes.",
        cs: "Data můžou jít o několik minut pozadu.");

    public static string TokensRecent => L(en: "Tokens (recent)", cs: "Tokeny (nedávné)");

    public static string TokenInput => L(en: "Input", cs: "Vstup");

    public static string TokenOutput => L(en: "Output", cs: "Výstup");

    public static string TokenCache => L(en: "Cache", cs: "Cache");

    public static string OnDemandOff => L(
        en: "On-demand off — no extra charges.",
        cs: "On-demand vypnuto — bez dalších poplatků.");

    public static string OnDemandOn => L(
        en: "On-demand on — extra spend is possible.",
        cs: "On-demand zapnuto — další útrata je možná.");

    public static string OpenSpendingDashboard => L(
        en: "Open spending dashboard",
        cs: "Otevřít spending dashboard");

    public static string SignInToCursor => L(en: "Sign in to Cursor", cs: "Přihlaste se do Cursoru");

    public static string UsageUnavailable => L(
        en: "Usage data unavailable",
        cs: "Data o využití nejsou dostupná");

    public static string RateLimited => L(en: "rate limited", cs: "rate limit");

    public static string Resets => L(en: "Resets", cs: "Reset");

    public static string AutostartFailed => L(
        en: "Could not change Start with Windows",
        cs: "Spouštění s Windows se nepodařilo nastavit");

    public static string AboutTagline => L(
        en: "A lightweight Windows desktop utility that shows current Cursor included usage in the notification area.",
        cs: "Lehká desktopová utilita pro Windows, která v oznamovací oblasti ukazuje aktuální included využití Cursoru.");

    public static string AboutCredit => L(
        en: "Developed by Martin Sladek with the help of AI models and workflows.",
        cs: "Vytvořil Martin Sladek s pomocí AI modelů a vývojových postupů.");

    public static string Website => L(en: "Website", cs: "Web");

    public static string GitHub => "GitHub";

    public static string TooltipIncluded(double total, double? auto, double? api)
    {
        string text = auto is double a && api is double p
            ? $"Included {FormatPercent(total)} · Auto {FormatPercent(a)} · API {FormatPercent(p)}"
            : $"Included {FormatPercent(total)}";
        return TruncateTooltip(text);
    }

    public static string TooltipIncludedResets(double total, DateTimeOffset end)
    {
        string date = end.ToLocalTime().ToString("d MMM", CultureInfo.CurrentCulture);
        return TruncateTooltip($"Included {FormatPercent(total)} · {Resets.ToLowerInvariant()} {date}");
    }

    public static string TooltipRateLimited(double total) =>
        TruncateTooltip($"Included {FormatPercent(total)} · {RateLimited}");

    public static string FormatPercent(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "—";

        double clamped = Math.Clamp(value, 0, 100);
        if (clamped < 10)
            return clamped.ToString("0.0", CultureInfo.CurrentCulture) + "%";

        return Math.Round(clamped).ToString("0", CultureInfo.CurrentCulture) + "%";
    }

    public static string FormatPlanName(string? membershipType)
    {
        if (string.IsNullOrWhiteSpace(membershipType))
            return "Pro";

        return string.Concat(
            char.ToUpperInvariant(membershipType[0]),
            membershipType.Length > 1 ? membershipType[1..] : "");
    }

    public static string TruncateTooltip(string text)
    {
        const int max = 63;
        if (text.Length <= max)
            return text;
        return text[..(max - 1)] + "…";
    }

    private static string L(string en, string cs) => Lang == "cs" ? cs : en;
}
