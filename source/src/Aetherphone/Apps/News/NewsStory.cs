using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.News;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.News;

internal sealed class NewsStory
{
    public LodestoneNewsItem Item = new();
    public DateTime LocalDay;
    public string DayLabel = string.Empty;
    public string Clock = string.Empty;
    public string LongDate = string.Empty;
    public string[] Paragraphs = Array.Empty<string>();
    public string Lead = string.Empty;
    public readonly NewsLines Title = new();
    public readonly NewsLines Summary = new();
    public readonly NewsLines HeroTitle = new();
    public CachedText Ago;
    public CachedText Countdown;
    public CachedText Window;
    public Spring Highlight;

    public string Id => Item.Id;

    public bool HasImage => !string.IsNullOrEmpty(Item.Image);

    public void Rebuild(LodestoneNewsItem item)
    {
        Item = item;
        var local = item.Time.ToLocalTime();
        LocalDay = local.Date;
        var unix = item.Time.ToUnixTimeSeconds();
        DayLabel = TimeText.DayLabel(unix);
        Clock = TimeText.Clock(local);
        LongDate = NewsFormat.LongDate(item.Time);
        Paragraphs = NewsFormat.Paragraphs(item.Description);
        Lead = Paragraphs.Length > 0 ? Paragraphs[0] : string.Empty;
        Ago.Reset();
        Countdown.Reset();
        Window.Reset();
    }

    public string AgoText()
    {
        var minutes = (long)(DateTimeOffset.UtcNow - Item.Time).TotalMinutes;
        return Ago.IsCurrent(minutes) ? Ago.Value : Ago.Store(minutes, TimeText.Ago(Item.Time));
    }

    public string WindowText(in MaintenanceWindow window)
    {
        var key = window.StartUnix ^ (window.EndUnix << 1);
        return Window.IsCurrent(key) ? Window.Value : Window.Store(key, NewsFormat.Window(window));
    }

    public string UntilText(long targetUnix, long nowUnix)
    {
        var minutes = Math.Max(0L, (targetUnix - nowUnix) / 60);
        return Countdown.IsCurrent(minutes)
            ? Countdown.Value
            : Countdown.Store(minutes, TimeText.Until(TimeSpan.FromMinutes(minutes + 1)));
    }

    public float StepHighlight(float target, float deltaSeconds) =>
        Highlight.Step(target, Motion.HoverLift, deltaSeconds);
}

internal sealed class NewsLines
{
    private const string Ellipsis = "…";

    private string source = string.Empty;
    private float width = -1f;
    private float fontKey = -1f;
    private int lineLimit = -1;
    private string[] lines = Array.Empty<string>();

    public string[] Get(string text, in TextStyle style, float maxWidth, int maxLines, float currentFontKey)
    {
        if (ReferenceEquals(text, source) && width == maxWidth && fontKey == currentFontKey && lineLimit == maxLines)
        {
            return lines;
        }

        source = text;
        width = maxWidth;
        fontKey = currentFontKey;
        lineLimit = maxLines;
        lines = Clamp(text, style, maxWidth, maxLines);
        return lines;
    }

    private static string[] Clamp(string text, in TextStyle style, float maxWidth, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0f || maxLines <= 0)
        {
            return Array.Empty<string>();
        }

        var wrapped = Typography.WrapText(text, style, maxWidth);
        if (wrapped.Length <= maxLines)
        {
            return wrapped;
        }

        var trimmed = new string[maxLines];
        Array.Copy(wrapped, trimmed, maxLines);
        trimmed[maxLines - 1] = Typography.FitText(trimmed[maxLines - 1] + Ellipsis, maxWidth, style);
        return trimmed;
    }
}
