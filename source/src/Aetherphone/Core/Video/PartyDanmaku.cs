using System.Buffers;
using System.Globalization;
using System.Text;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Video;

internal delegate float DanmakuMeasure(string text);

internal readonly record struct DanmakuLine(string UserId, string Name, string Text, bool Own, long StartedAtTicks,
    int Lane, float NameWidth, float Width);

internal sealed class PartyDanmaku
{
    internal const int MaxTextLength = 60;
    internal const int MinimumLanes = 1;
    internal const int MaximumLanes = 5;
    internal const int DefaultLanes = 3;
    internal const int LinesPerLane = 4;
    internal const int WaitingCapacity = 30;
    internal const long TravelMilliseconds = 8000;
    internal const int LanesPerScreenHeight = 12;
    internal const float TextShareOfLane = 0.8f;
    internal const float NameGap = 0.5f;
    internal const float LineGap = 1f;

    // Widths are kept in font heights so every surface schedules alike. Text never stands taller than its share of a
    // lane, and a lane is never taller than a 16:9 band allows, so a screen is at least this many font heights wide.
    internal const float BandWidth = 16f / 9f * LanesPerScreenHeight / TextShareOfLane;

    private readonly DanmakuLine[] active = new DanmakuLine[MaximumLanes * LinesPerLane];
    private readonly DanmakuLine[] waiting = new DanmakuLine[WaitingCapacity];
    private int waitingStart;

    internal ReadOnlySpan<DanmakuLine> Active => active;

    internal int WaitingCount { get; private set; }

    internal static bool CanSend(int serverFeatures, bool roomEnabled) =>
        (serverFeatures & StreamFeature.Danmaku) != 0 && roomEnabled;

    internal static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var buffer = new char[value.Length];
        var length = 0;
        var index = 0;
        while (index < value.Length)
        {
            var status = Rune.DecodeFromUtf16(value.AsSpan(index), out var rune, out var consumed);
            index += consumed;
            if (status != OperationStatus.Done || Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
                    or UnicodeCategory.Format or UnicodeCategory.PrivateUse)
            {
                continue;
            }

            length += rune.EncodeToUtf16(buffer.AsSpan(length));
        }

        var trimmed = new string(buffer, 0, length).Trim();
        var runes = 0;
        var end = 0;
        foreach (var rune in trimmed.EnumerateRunes())
        {
            if (runes == MaxTextLength)
            {
                break;
            }

            end += rune.Utf16SequenceLength;
            runes++;
        }

        return end == trimmed.Length ? trimmed : trimmed[..end];
    }

    internal static bool TryProgress(in DanmakuLine line, long nowTicks, out float progress)
    {
        progress = 0f;
        if (line.StartedAtTicks == 0)
        {
            return false;
        }

        var elapsed = nowTicks - line.StartedAtTicks;
        if (elapsed < 0 || elapsed >= TravelMilliseconds)
        {
            return false;
        }

        progress = elapsed / (float)TravelMilliseconds;
        return true;
    }

    internal void Enqueue(string userId, string name, string text, bool own)
    {
        if (WaitingCount == WaitingCapacity)
        {
            waiting[waitingStart] = default;
            waitingStart = (waitingStart + 1) % WaitingCapacity;
            WaitingCount--;
        }

        waiting[(waitingStart + WaitingCount) % WaitingCapacity] = new DanmakuLine(userId, name, text, own, 0, -1, 0f, 0f);
        WaitingCount++;
    }

    internal void Release(long nowTicks, int lanes, DanmakuMeasure measure)
    {
        lanes = Math.Clamp(lanes, MinimumLanes, MaximumLanes);
        var live = 0;
        for (var index = 0; index < active.Length; index++)
        {
            if (active[index].StartedAtTicks == 0)
            {
                continue;
            }

            if (nowTicks - active[index].StartedAtTicks >= TravelMilliseconds)
            {
                active[index] = default;
                continue;
            }

            live++;
        }

        while (WaitingCount > 0 && live < lanes * LinesPerLane)
        {
            var head = waiting[waitingStart];
            if (head.Width <= 0f)
            {
                var nameWidth = measure(head.Name);
                head = head with { NameWidth = nameWidth, Width = nameWidth + NameGap + measure(head.Text) };
                waiting[waitingStart] = head;
            }

            var lane = FreeLane(nowTicks, lanes, head.Width);
            if (lane < 0)
            {
                return;
            }

            active[FreeSlot()] = head with { StartedAtTicks = nowTicks, Lane = lane };
            waiting[waitingStart] = default;
            waitingStart = (waitingStart + 1) % WaitingCapacity;
            WaitingCount--;
            live++;
        }
    }

    internal void Hide(string userId)
    {
        for (var index = 0; index < active.Length; index++)
        {
            if (active[index].StartedAtTicks != 0 && string.Equals(active[index].UserId, userId, StringComparison.Ordinal))
            {
                active[index] = default;
            }
        }

        var kept = 0;
        for (var offset = 0; offset < WaitingCount; offset++)
        {
            var line = waiting[(waitingStart + offset) % WaitingCapacity];
            if (string.Equals(line.UserId, userId, StringComparison.Ordinal))
            {
                continue;
            }

            waiting[(waitingStart + kept) % WaitingCapacity] = line;
            kept++;
        }

        for (var offset = kept; offset < WaitingCount; offset++)
        {
            waiting[(waitingStart + offset) % WaitingCapacity] = default;
        }

        WaitingCount = kept;
    }

    internal void Clear()
    {
        Array.Clear(active);
        Array.Clear(waiting);
        waitingStart = 0;
        WaitingCount = 0;
    }

    private int FreeLane(long nowTicks, int lanes, float width)
    {
        for (var lane = 0; lane < lanes; lane++)
        {
            if (LaneAccepts(lane, nowTicks, width))
            {
                return lane;
            }
        }

        return -1;
    }

    // A line enters once the one ahead has fully cleared the right edge, and only if, moving faster when it is longer,
    // it would not reach that line's tail before the line ahead leaves the screen.
    private bool LaneAccepts(int lane, long nowTicks, float width)
    {
        var latest = -1;
        for (var index = 0; index < active.Length; index++)
        {
            var line = active[index];
            if (line.StartedAtTicks != 0 && line.Lane == lane
                && (latest < 0 || line.StartedAtTicks > active[latest].StartedAtTicks))
            {
                latest = index;
            }
        }

        if (latest < 0)
        {
            return true;
        }

        var ahead = active[latest];
        var travelled = (nowTicks - ahead.StartedAtTicks) / (float)TravelMilliseconds;
        var cleared = travelled * (BandWidth + ahead.Width) >= ahead.Width + LineGap;
        var catchesUp = (BandWidth + width) * (1f - travelled) > BandWidth;
        return cleared && !catchesUp;
    }

    private int FreeSlot()
    {
        for (var index = 0; index < active.Length; index++)
        {
            if (active[index].StartedAtTicks == 0)
            {
                return index;
            }
        }

        return 0;
    }
}
