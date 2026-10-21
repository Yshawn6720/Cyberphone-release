using System.Collections.Frozen;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Coins;

internal static class CoinRuleLabels
{
    private static readonly FrozenDictionary<string, LocString> Labels = new Dictionary<string, LocString>
    {
        ["coin.checkin"] = L.Coin.RuleCheckin,
        ["coin.streak"] = L.Coin.RuleStreak,
        ["coin.welcome"] = L.Coin.RuleWelcome,
        ["call.connected"] = L.Coin.RuleCall,
        ["chat.conversation"] = L.Coin.RuleChat,
        ["game.session"] = L.Coin.RuleGameSession,
        ["game.deep"] = L.Coin.RuleGameDeep,
        ["game.featured"] = L.Coin.RuleGameFeatured,
        ["chirp.survived"] = L.Coin.RuleChirp,
        ["gram.survived"] = L.Coin.RuleGram,
        ["story.survived"] = L.Coin.RuleStory,
        ["comment.survived"] = L.Coin.RuleComment,
        ["post.survived"] = L.Coin.RulePost,
        ["comment.daily"] = L.Coin.RuleCommentsDaily,
        ["quest.daily"] = L.Coin.RuleQuest,
        ["purchase"] = L.Coin.RulePurchase,
        ["staff.adjust"] = L.Coin.RuleStaffGrant,
        ["staff.clawback"] = L.Coin.RuleClawback,
        ["carry.forward"] = L.Coin.RuleCarry,
        [Casino.CasinoLedgerRules.BuyIn] = L.Coin.RuleCasinoBuyIn,
        [Casino.CasinoLedgerRules.CashOut] = L.Coin.RuleCasinoCashOut,
        [Casino.CasinoLedgerRules.Refund] = L.Coin.RuleCasinoRefund,
        [Casino.CasinoLedgerRules.Daily] = L.Coin.RuleCasinoDaily,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, LocString> Hints = new Dictionary<string, LocString>
    {
        ["coin.checkin"] = L.Coin.RuleCheckinHint,
        ["coin.streak"] = L.Coin.RuleStreakHint,
        ["coin.welcome"] = L.Coin.RuleWelcomeHint,
        ["call.connected"] = L.Coin.RuleCallHint,
        ["chat.conversation"] = L.Coin.RuleChatHint,
        ["game.session"] = L.Coin.RuleGameSessionHint,
        ["game.deep"] = L.Coin.RuleGameDeepHint,
        ["game.featured"] = L.Coin.RuleGameFeaturedHint,
        ["chirp.survived"] = L.Coin.RuleChirpHint,
        ["gram.survived"] = L.Coin.RuleGramHint,
        ["story.survived"] = L.Coin.RuleStoryHint,
        ["comment.survived"] = L.Coin.RuleCommentHint,
        ["post.survived"] = L.Coin.RulePostHint,
        ["comment.daily"] = L.Coin.RuleCommentsDailyHint,
        ["quest.daily"] = L.Coin.RuleQuestHint,
        [Casino.CasinoLedgerRules.Daily] = L.Coin.RuleCasinoDailyHint,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static LocString For(string ruleId)
    {
        return Labels.TryGetValue(ruleId, out var label) ? label : L.Coin.RuleGeneric;
    }

    public static bool TryHint(string ruleId, out LocString hint)
    {
        return Hints.TryGetValue(ruleId, out hint);
    }
}
