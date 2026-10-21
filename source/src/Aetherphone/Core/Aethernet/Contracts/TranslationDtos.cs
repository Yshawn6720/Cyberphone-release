namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record TranslateBatchItem(string Id, string Text, string Surface, bool Cacheable);

internal sealed record TranslateBatchRequest(string TargetLang, TranslateBatchItem[] Items);

internal sealed record TranslateBatchResult(string Id, string Status, string? Text, string? SourceLang, bool Cached);

internal sealed record TranslateBatchResponse(TranslateBatchResult[] Results, int RemainingToday, int DailyLimit);

internal sealed record TranslateStatusResponse(bool Enabled, int RemainingToday, int DailyLimit, string[] TargetLangs);

internal static class TranslateStatuses
{
    public const string Ok = "ok";
    public const string SameLanguage = "same_language";
    public const string Quota = "quota";
    public const string GlobalQuota = "global_quota";
    public const string Unavailable = "unavailable";
}
