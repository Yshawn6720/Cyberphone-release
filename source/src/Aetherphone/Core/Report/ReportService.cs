using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Report;

internal enum ReportVenue : byte
{
    General,
    Social,
    Velvet,
}

internal readonly record struct ReportReason(string Category, string Details);

internal sealed class ReportPrompt
{
    public required string Title;
    public string? Disclosure;
    public ReportVenue Venue = ReportVenue.General;
    public required Action<ReportReason, Action<bool>> Submit;
}

internal static class ReportCategories
{
    private const byte General = 1 << (int)ReportVenue.General;
    private const byte Social = 1 << (int)ReportVenue.Social;
    private const byte Velvet = 1 << (int)ReportVenue.Velvet;
    private const byte Everywhere = General | Social | Velvet;

    public readonly record struct Entry(string Id, LocString Label, LocString Summary, byte Venues)
    {
        public bool ShowsIn(ReportVenue venue) => (Venues & (1 << (int)venue)) != 0;
    }

    public static readonly Entry[] All =
    {
        new("spam", L.Report.CategorySpam, L.Report.SummarySpam, Everywhere),
        new("harassment", L.Report.CategoryHarassment, L.Report.SummaryHarassment, Everywhere),
        new("hate", L.Report.CategoryHateSpeech, L.Report.SummaryHate, Everywhere),
        new("threats", L.Report.CategoryThreats, L.Report.SummaryThreats, Everywhere),
        new("explicit", L.Report.CategoryExplicit, L.Report.SummaryExplicit, General | Social),
        new("explicit", L.Report.CategoryVelvetProhibited, L.Report.SummaryVelvetProhibited, Velvet),
        new("consent", L.Report.CategoryConsent, L.Report.SummaryConsent, Velvet),
        new("ingame", L.Report.CategoryInGame, L.Report.SummaryInGame, Social),
        new("creators", L.Report.CategoryCreators, L.Report.SummaryCreators, Everywhere),
        new("impersonation", L.Report.CategoryImpersonation, L.Report.SummaryImpersonation, Everywhere),
        new("privacy", L.Report.CategoryPrivacy, L.Report.SummaryPrivacy, Everywhere),
        new("selfharm", L.Report.CategorySelfHarm, L.Report.SummarySelfHarm, Everywhere),
        new("child", L.Report.CategoryChildSafety, L.Report.SummaryChildSafety, Everywhere),
        new("exploitation", L.Report.CategoryExploitation, L.Report.SummaryExploitation, Everywhere),
        new("evasion", L.Report.CategoryEvasion, L.Report.SummaryEvasion, Everywhere),
        new("other", L.Report.CategoryOther, L.Report.SummaryOther, Everywhere),
    };

    public static int Collect(ReportVenue venue, Span<int> visible)
    {
        var count = 0;
        for (var index = 0; index < All.Length; index++)
        {
            if (All[index].ShowsIn(venue))
            {
                visible[count++] = index;
            }
        }

        return count;
    }
}

internal sealed class ReportService
{
    private readonly int[] visible = new int[ReportCategories.All.Length];

    public ReportPrompt? Active { get; private set; }
    public int CategoryIndex = -1;
    public string ReasonDraft = string.Empty;
    public volatile bool Busy;
    public bool Sent { get; private set; }
    public bool Failed { get; private set; }
    public int VisibleCount { get; private set; }

    public ReadOnlySpan<int> Visible => visible.AsSpan(0, VisibleCount);

    public void Open(ReportPrompt prompt)
    {
        Active = prompt;
        VisibleCount = ReportCategories.Collect(prompt.Venue, visible);
        CategoryIndex = -1;
        ReasonDraft = string.Empty;
        Busy = false;
        Sent = false;
        Failed = false;
    }

    public void Submit()
    {
        if (Active is not { } prompt || Busy || Sent || CategoryIndex < 0)
        {
            return;
        }

        Busy = true;
        Failed = false;
        var reason = new ReportReason(ReportCategories.All[CategoryIndex].Id, ReasonDraft.Trim());
        prompt.Submit(reason, ok =>
        {
            Busy = false;
            Sent = ok;
            Failed = !ok;
        });
    }

    public void Dismiss()
    {
        if (Busy)
        {
            return;
        }

        Active = null;
    }
}
