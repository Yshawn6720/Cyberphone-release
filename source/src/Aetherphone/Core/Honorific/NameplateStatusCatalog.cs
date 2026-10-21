using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Core.Honorific;

internal readonly record struct NameplateStatusInfo(
    NameplateStatus Status,
    FontAwesomeIcon Icon,
    Vector4 Tint,
    LocString Label,
    LocString Hint,
    LocString? DefaultTemplate,
    string Tokens,
    NameplateStatus Parent = NameplateStatus.None);

internal static class NameplateStatusCatalog
{
    public const string CodeToken = "[code]";
    public const string NameToken = "[name]";
    public const string StationToken = "[station]";
    public const string TypeToken = "[type]";
    public const string HandleToken = "[handle]";
    public const string SongToken = "[song]";
    public const string ArtistToken = "[artist]";
    public const string GameToken = "[game]";
    public const string ChipsToken = "[chips]";

    private static readonly Vector4 CustomTint = new(0.11f, 0.50f, 0.58f, 1f);
    private static readonly Vector4 BusyTint = new(0.45f, 0.47f, 0.55f, 1f);
    private static readonly Vector4 QuietTint = new(0.40f, 0.36f, 0.78f, 1f);

    public static readonly NameplateStatusInfo[] All =
    {
        new(NameplateStatus.Chirper, FontAwesomeIcon.At, AppAccents.For("chirper"), L.Apps.Chirper,
            L.Nameplate.SocialAppsHint, L.Nameplate.TemplateChirper, HandleToken),
        new(NameplateStatus.Aethergram, FontAwesomeIcon.At, AppAccents.For("aethergram"), L.Apps.Aethergram,
            L.Nameplate.SocialAppsHint, L.Nameplate.TemplateAethergram, HandleToken),
        new(NameplateStatus.Velvet, FontAwesomeIcon.Heart, AppAccents.For("velvet"), L.Apps.Velvet,
            L.Nameplate.VelvetHint, L.Nameplate.TemplateVelvet, HandleToken),
        new(NameplateStatus.Games, FontAwesomeIcon.Gamepad, AppAccents.For("games"), L.Apps.Games,
            L.Nameplate.GamesHint, L.Nameplate.TemplateGames, GameToken),
        new(NameplateStatus.SlotsWin, FontAwesomeIcon.Coins, AccentRing.Gold, L.Nameplate.SlotsWin,
            L.Nameplate.SlotsWinHint, L.Nameplate.TemplateSlotsWin, ChipsToken, NameplateStatus.Gamba),
        new(NameplateStatus.SlotsLoss, FontAwesomeIcon.SadTear, AccentRing.Slate, L.Nameplate.SlotsLoss,
            L.Nameplate.SlotsLossHint, L.Nameplate.TemplateSlotsLoss, ChipsToken, NameplateStatus.Gamba),
        new(NameplateStatus.Gamba, FontAwesomeIcon.Dice, AppAccents.For("casino"), L.Apps.Casino,
            L.Nameplate.GambaHint, L.Nameplate.TemplateGamba, GameToken),
        new(NameplateStatus.MogCast, FontAwesomeIcon.Tv, AppAccents.For("aetherstream"), L.Nameplate.MogCast,
            L.Nameplate.MogCastHint, L.Nameplate.TemplateMogCast, CodeToken),
        new(NameplateStatus.Jam, FontAwesomeIcon.Music, AppAccents.For("music"), L.Nameplate.JamRow,
            L.Nameplate.JamHint, L.Nameplate.TemplateJam, CodeToken + ", " + NameToken),
        new(NameplateStatus.Muster, FontAwesomeIcon.Users, AppAccents.For("muster"), L.Nameplate.Muster,
            L.Nameplate.MusterHint, L.Nameplate.TemplateMuster, TypeToken),
        new(NameplateStatus.NowPlaying, FontAwesomeIcon.Headphones, AppAccents.For("music"), L.Nameplate.NowPlaying,
            L.Nameplate.NowPlayingHint, L.Nameplate.TemplateNowPlaying, SongToken + ", " + ArtistToken),
        new(NameplateStatus.RadioOnAir, FontAwesomeIcon.BroadcastTower, AccentRing.Orange, L.Nameplate.Radio,
            L.Nameplate.RadioHint, L.Nameplate.TemplateRadio, StationToken),
        new(NameplateStatus.InCall, FontAwesomeIcon.Phone, BusyTint, L.Nameplate.InCall, L.Nameplate.InCallHint,
            L.Nameplate.TemplateInCall, string.Empty),
        new(NameplateStatus.DoNotDisturb, FontAwesomeIcon.Moon, QuietTint, L.Nameplate.DoNotDisturb,
            L.Nameplate.DoNotDisturbHint, L.Nameplate.TemplateDoNotDisturb, string.Empty),
        new(NameplateStatus.Handle, FontAwesomeIcon.UserTag, AppAccents.For("aethergram"), L.Nameplate.Handle,
            L.Nameplate.HandleHint, L.Nameplate.TemplateHandle, HandleToken),
        new(NameplateStatus.Custom, FontAwesomeIcon.Pen, CustomTint, L.Nameplate.Custom, L.Nameplate.CustomHint, null,
            string.Empty),
    };

    public static readonly NameplateStatus[] Ranked = BuildRanked();

    private const NameplateStatus AethernetStatuses = NameplateStatus.Chirper | NameplateStatus.Aethergram |
        NameplateStatus.Velvet | NameplateStatus.Handle | NameplateStatus.Gamba | NameplateStatus.SlotsWin |
        NameplateStatus.SlotsLoss | NameplateStatus.Muster | NameplateStatus.Jam | NameplateStatus.RadioOnAir |
        NameplateStatus.MogCast | NameplateStatus.InCall;

    public static bool IsHidden(NameplateStatus status) =>
        !HiddenApps.AethernetEnabled && (AethernetStatuses & status) != 0;

    public static int IndexOf(NameplateStatus status)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (All[index].Status == status)
            {
                return index;
            }
        }

        return -1;
    }

    public static ref readonly NameplateStatusInfo For(NameplateStatus status)
    {
        var index = IndexOf(status);
        return ref All[index < 0 ? All.Length - 1 : index];
    }

    public static int ChildrenOf(NameplateStatus parent, Span<NameplateStatus> children)
    {
        var count = 0;
        for (var index = 0; index < All.Length && count < children.Length; index++)
        {
            if (All[index].Parent == parent)
            {
                children[count] = All[index].Status;
                count++;
            }
        }

        return count;
    }

    private static NameplateStatus[] BuildRanked()
    {
        var count = 0;
        for (var index = 0; index < All.Length; index++)
        {
            if (All[index].Parent == NameplateStatus.None)
            {
                count++;
            }
        }

        var ranked = new NameplateStatus[count];
        count = 0;
        for (var index = 0; index < All.Length; index++)
        {
            if (All[index].Parent == NameplateStatus.None)
            {
                ranked[count] = All[index].Status;
                count++;
            }
        }

        return ranked;
    }
}
