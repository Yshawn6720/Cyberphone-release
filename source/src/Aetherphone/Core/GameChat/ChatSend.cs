using System.Text;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.GameChat;

internal sealed class PendingSend
{
    public required string ChannelKey { get; init; }
    public required string Target { get; init; }
    public required string Text { get; init; }
    public required long SentAtMilliseconds { get; init; }
    public bool Failed { get; set; }
}

internal sealed class ChatSend
{
    public const int MaxBytes = ChatSender.MaxBytes;
    public const int MinimumIntervalMilliseconds = 250;
    public const int MaximumIntervalMilliseconds = 5000;
    private const long ResolveWindowMilliseconds = 10000;
    private const int MaxFailed = 8;

    private readonly List<PendingSend> pending = new();
    private readonly List<QueuedPart> queue = new(MessageSplitter.MaxParts);
    private readonly List<string> parts = new(MessageSplitter.MaxParts);
    private readonly IFramework.OnUpdateDelegate drain;
    private long nextPartMilliseconds;
    private long partIntervalMilliseconds = MinimumIntervalMilliseconds;
    private bool draining;

    public ChatSend() => drain = OnFrameworkUpdate;

    public IReadOnlyList<PendingSend> Pending => pending;

    public int Queued => queue.Count;

    public static int Budget(GameChannel channel, string target)
    {
        if (!channel.CanSend)
        {
            return 0;
        }

        var prefix = Encoding.UTF8.GetByteCount(channel.Command) + 1;
        if (channel.NeedsTarget)
        {
            prefix += Encoding.UTF8.GetByteCount(target) + 1;
        }

        return Math.Max(0, MaxBytes - prefix);
    }

    public bool Send(GameChannel channel, string target, string text)
    {
        Tick();
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || !channel.CanSend)
        {
            return false;
        }

        if (channel.NeedsTarget && target.Length == 0)
        {
            return false;
        }

        var line = channel.NeedsTarget
            ? string.Concat(channel.Command, " ", target, " ", trimmed)
            : string.Concat(channel.Command, " ", trimmed);
        var entry = new PendingSend
        {
            ChannelKey = channel.Key,
            Target = target,
            Text = trimmed,
            SentAtMilliseconds = Environment.TickCount64,
        };
        pending.Add(entry);
        if (ChatSender.TrySend(line))
        {
            return true;
        }

        pending.Remove(entry);
        return false;
    }

    public bool SendSplit(GameChannel channel, string target, string text, string indicator, int intervalMilliseconds)
    {
        Tick();
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || !channel.CanSend)
        {
            return false;
        }

        if (channel.NeedsTarget && target.Length == 0)
        {
            return false;
        }

        MessageSplitter.Split(trimmed, Budget(channel, target), indicator, parts);
        if (parts.Count == 0)
        {
            return false;
        }

        if (!Send(channel, target, parts[0]))
        {
            return false;
        }

        if (parts.Count == 1)
        {
            return true;
        }

        partIntervalMilliseconds =
            Math.Clamp(intervalMilliseconds, MinimumIntervalMilliseconds, MaximumIntervalMilliseconds);
        nextPartMilliseconds = Environment.TickCount64 + partIntervalMilliseconds;
        for (var index = 1; index < parts.Count; index++)
        {
            queue.Add(new QueuedPart(channel, target, parts[index]));
        }

        StartDraining();
        return true;
    }

    public void ClearQueue()
    {
        queue.Clear();
        StopDraining();
    }

    public bool TryResolve(string channelKey, string text)
    {
        Tick();
        for (var index = 0; index < pending.Count; index++)
        {
            var candidate = pending[index];
            if (!string.Equals(candidate.ChannelKey, channelKey, StringComparison.Ordinal) ||
                !string.Equals(candidate.Text, text, StringComparison.Ordinal))
            {
                continue;
            }

            pending.RemoveAt(index);
            return true;
        }

        return false;
    }

    public void Tick()
    {
        var now = Environment.TickCount64;
        var failed = 0;
        for (var index = pending.Count - 1; index >= 0; index--)
        {
            var candidate = pending[index];
            if (!candidate.Failed && now - candidate.SentAtMilliseconds >= ResolveWindowMilliseconds)
            {
                candidate.Failed = true;
            }

            if (!candidate.Failed)
            {
                continue;
            }

            failed++;
            if (failed > MaxFailed)
            {
                pending.RemoveAt(index);
            }
        }
    }

    public void Discard(PendingSend entry) => pending.Remove(entry);

    public void Clear(string channelKey)
    {
        for (var index = pending.Count - 1; index >= 0; index--)
        {
            if (string.Equals(pending[index].ChannelKey, channelKey, StringComparison.Ordinal))
            {
                pending.RemoveAt(index);
            }
        }

        for (var index = queue.Count - 1; index >= 0; index--)
        {
            if (string.Equals(queue[index].Channel.Key, channelKey, StringComparison.Ordinal))
            {
                queue.RemoveAt(index);
            }
        }

        if (queue.Count == 0)
        {
            StopDraining();
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (queue.Count == 0)
        {
            StopDraining();
            return;
        }

        if (Plugin.ClientState is { IsLoggedIn: false })
        {
            ClearQueue();
            return;
        }

        var now = Environment.TickCount64;
        if (now < nextPartMilliseconds)
        {
            return;
        }

        var part = queue[0];
        queue.RemoveAt(0);
        if (!Send(part.Channel, part.Target, part.Text))
        {
            ClearQueue();
            return;
        }

        nextPartMilliseconds = now + partIntervalMilliseconds;
        if (queue.Count == 0)
        {
            StopDraining();
        }
    }

    private void StartDraining()
    {
        if (draining || Plugin.Framework is null)
        {
            return;
        }

        Plugin.Framework.Update += drain;
        draining = true;
    }

    private void StopDraining()
    {
        if (!draining)
        {
            return;
        }

        Plugin.Framework.Update -= drain;
        draining = false;
    }

    private readonly struct QueuedPart
    {
        public readonly GameChannel Channel;
        public readonly string Target;
        public readonly string Text;

        public QueuedPart(GameChannel channel, string target, string text)
        {
            Channel = channel;
            Target = target;
            Text = text;
        }
    }
}
