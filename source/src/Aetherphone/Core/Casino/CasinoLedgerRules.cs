namespace Aetherphone.Core.Casino;

internal static class CasinoLedgerRules
{
    public const string BuyIn = "casino.buyin";
    public const string CashOut = "casino.cashout";
    public const string Refund = "casino.refund";
    public const string Daily = "casino.daily";

    private const string RulePrefix = "casino.";

    public static bool SkipsEarnCelebration(string ruleId)
    {
        if (string.Equals(ruleId, Daily, StringComparison.Ordinal))
        {
            return false;
        }

        return ruleId.StartsWith(RulePrefix, StringComparison.Ordinal);
    }
}
