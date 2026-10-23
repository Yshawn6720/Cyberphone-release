using System.Collections.Concurrent;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Video;

internal static class PartyCode
{
    internal const int Length = 6;
    internal const int MaxInputLength = 16;

    private const int GroupLength = 3;

    internal static string Normalize(string? input)
    {
        if (input is not { Length: > 0 and <= MaxInputLength })
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[input.Length];
        var count = 0;
        for (var index = 0; index < input.Length; index++)
        {
            var character = char.ToUpperInvariant(input[index]);
            if (char.IsAsciiLetterOrDigit(character))
            {
                buffer[count] = character;
                count++;
            }
        }

        return count == Length ? new string(buffer[..count]) : string.Empty;
    }

    internal static string Display(string code) =>
        code.Length == Length ? string.Concat(code.AsSpan(0, GroupLength), " ", code.AsSpan(GroupLength)) : code;
}

internal static class PartyPermissions
{
    internal static int Held(int guestPermissions, StreamMember[]? members, string userId)
    {
        var held = guestPermissions;
        if (members is null)
        {
            return held;
        }

        for (var index = 0; index < members.Length; index++)
        {
            if (string.Equals(members[index].UserId, userId, StringComparison.Ordinal))
            {
                return held | members[index].Flags;
            }
        }

        return held;
    }

    internal static bool Allows(int held, int permission) => (held & permission) != 0;

    internal static int Required(string action) =>
        action == StreamControlAction.Load ? StreamPermission.SwitchVideo : StreamPermission.ControlPlayback;

    internal static int Published(int permissions, int serverFeatures) =>
        (serverFeatures & StreamFeature.SwitchVideo) != 0 ? permissions : permissions & ~StreamPermission.SwitchVideo;
}

internal static class PartySwitch
{
    internal static string? Link(string input)
    {
        if (input.Length == 0 || MediaInput.LooksLikeLocalPath(input) || !VideoEngine.ValidateURL(input, out var url)
            || url is null)
        {
            return null;
        }

        var link = url.OriginalString;
        return VideoUrlResolver.IsPlaylistUrl(link) && !VideoUrlResolver.NamesOneVideo(link) ? null : link;
    }
}

internal sealed class PartyGrants
{
    private readonly ConcurrentDictionary<string, int> flags = new(StringComparer.Ordinal);

    internal bool IsEmpty => flags.IsEmpty;

    internal int For(string userId) => flags.GetValueOrDefault(userId);

    internal void Set(string userId, int permission, bool granted)
    {
        var held = For(userId);
        var updated = (granted ? held | permission : held & ~permission) & StreamPermission.GrantMask;
        if (updated == 0)
        {
            flags.TryRemove(userId, out _);
            return;
        }

        flags[userId] = updated;
    }

    internal bool Forget(string userId) => flags.TryRemove(userId, out _);

    internal void Clear() => flags.Clear();

    internal void Inherit(StreamMember[]? members, string? selfId)
    {
        flags.Clear();
        if (members is null)
        {
            return;
        }

        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            var granted = member.Flags & StreamPermission.GrantMask;
            if (granted != 0 && !string.Equals(member.UserId, selfId, StringComparison.Ordinal))
            {
                flags[member.UserId] = granted;
            }
        }
    }

    internal StreamMember[]? Snapshot()
    {
        if (flags.IsEmpty)
        {
            return null;
        }

        var members = new List<StreamMember>(flags.Count);
        foreach (var pair in flags)
        {
            members.Add(new StreamMember(pair.Key, pair.Value));
        }

        return members.ToArray();
    }
}

internal readonly record struct PartyPolicy(bool ApprovalRequired, bool Discoverable, bool CodeEnabled,
    int GuestPermissions, bool DanmakuEnabled)
{
    internal const int GuestMask = StreamPermission.AddToQueue | StreamPermission.ControlPlayback
        | StreamPermission.SwitchVideo;

    internal bool GuestsCanAdd => PartyPermissions.Allows(GuestPermissions, StreamPermission.AddToQueue);

    internal bool GuestsCanControl => PartyPermissions.Allows(GuestPermissions, StreamPermission.ControlPlayback);

    internal bool GuestsCanSwitch => PartyPermissions.Allows(GuestPermissions, StreamPermission.SwitchVideo);

    internal PartyPolicy WithGuestPermission(int permission, bool allowed) =>
        this with { GuestPermissions = allowed ? GuestPermissions | permission : GuestPermissions & ~permission };

    internal static PartyPolicy From(Configuration configuration) => new(
        configuration.VideoStreamApprovalRequired,
        configuration.VideoStreamDiscoverable,
        configuration.VideoPartyCodeEnabled,
        (configuration.VideoPartyGuestsCanAdd ? StreamPermission.AddToQueue : 0)
        | (configuration.VideoPartyGuestsCanControl ? StreamPermission.ControlPlayback : 0)
        | (configuration.VideoPartyGuestsCanSwitch ? StreamPermission.SwitchVideo : 0),
        configuration.VideoPartyDanmakuEnabled);

    internal void Store(Configuration configuration)
    {
        configuration.VideoStreamApprovalRequired = ApprovalRequired;
        configuration.VideoStreamDiscoverable = Discoverable;
        configuration.VideoPartyCodeEnabled = CodeEnabled;
        configuration.VideoPartyGuestsCanAdd = GuestsCanAdd;
        configuration.VideoPartyGuestsCanControl = GuestsCanControl;
        configuration.VideoPartyGuestsCanSwitch = GuestsCanSwitch;
        configuration.VideoPartyDanmakuEnabled = DanmakuEnabled;
        configuration.Save();
    }
}

internal static class PartyIdle
{
    internal const long GraceMilliseconds = 5 * 60 * 1000;

    internal static float RemainingSeconds(long idleSinceTicks, long nowTicks)
    {
        if (idleSinceTicks == 0)
        {
            return 0f;
        }

        return Math.Max(0f, (GraceMilliseconds - (nowTicks - idleSinceTicks)) / 1000f);
    }

    internal static bool Expired(long idleSinceTicks, long nowTicks) =>
        idleSinceTicks != 0 && nowTicks - idleSinceTicks >= GraceMilliseconds;
}

internal enum PartySilenceAction : byte
{
    None,
    Reconnect,
    Leave,
}

internal static class PartySilence
{
    internal const long LimitMilliseconds = 90_000;

    internal static PartySilenceAction Check(long lastHeardTicks, long nowTicks, long hostAwayUntilTicks,
        long reconnectedAtTicks)
    {
        if (lastHeardTicks == 0)
        {
            return PartySilenceAction.None;
        }

        var since = Math.Max(Math.Max(lastHeardTicks, reconnectedAtTicks), hostAwayUntilTicks);
        if (nowTicks - since < LimitMilliseconds)
        {
            return PartySilenceAction.None;
        }

        return reconnectedAtTicks > lastHeardTicks ? PartySilenceAction.Leave : PartySilenceAction.Reconnect;
    }
}

internal static class PartyFollow
{
    internal const double EndGuardSeconds = 10.0;

    internal static bool ShouldReopen(string? roomUrl, string viewingUrl, double viewerDurationSeconds,
        double hostPositionSeconds, bool viewerLive) =>
        string.Equals(roomUrl, viewingUrl, StringComparison.Ordinal)
        && (viewerLive || viewerDurationSeconds <= 0d
            || hostPositionSeconds < viewerDurationSeconds - EndGuardSeconds);
}

internal static class PartyHostPosition
{
    internal static double Report(string url, string? publishedUrl, bool restarting, bool positionUnavailable,
        double position, double expectedPosition) =>
        (restarting || positionUnavailable) && string.Equals(url, publishedUrl, StringComparison.Ordinal)
            ? expectedPosition
            : position;
}

internal static class PartyViewerSync
{
    internal static bool CanSync(bool playing, bool hostPaused, bool live, bool restarting, bool positionUnavailable) =>
        playing && !hostPaused && !live && !restarting && !positionUnavailable;
}

internal sealed class PartyResyncBudget
{
    internal const long WindowMilliseconds = 60 * 1000;

    private string? lastUrl;
    private long lastAtTicks;

    internal bool TryTake(string url, long nowTicks)
    {
        if (string.Equals(url, lastUrl, StringComparison.Ordinal) && nowTicks - lastAtTicks < WindowMilliseconds)
        {
            return false;
        }

        lastUrl = url;
        lastAtTicks = nowTicks;
        return true;
    }
}

internal static class MediaInput
{
    internal static string Normalize(string? raw)
    {
        if (raw is null)
        {
            return string.Empty;
        }

        var trimmed = raw.Trim();
        while (trimmed.Length >= 2 && IsQuote(trimmed[0]) && trimmed[^1] == trimmed[0])
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return trimmed;
    }

    internal static bool LooksLikeLocalPath(string input)
    {
        if (input.Length < 3 || LocalMediaToken.IsToken(input))
        {
            return false;
        }

        if (input.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        return char.IsAsciiLetter(input[0]) && input[1] == ':' && (input[2] == '\\' || input[2] == '/');
    }

    internal static string Subtitle(string source, double? durationSeconds)
    {
        if (durationSeconds is not { } seconds || seconds <= 0d)
        {
            return source;
        }

        var clock = Localization.TimeText.MinutesSeconds((int)seconds);
        return source.Length > 0 ? string.Concat(source, "  ·  ", clock) : clock;
    }

    private static bool IsQuote(char character) => character is '"' or '\'';
}
