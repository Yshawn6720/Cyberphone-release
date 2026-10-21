namespace Aetherphone.Core.GameChat;

internal sealed class ChannelStyleStore
{
    private readonly Configuration configuration;
    private readonly ChannelStyle?[] byIndex;
    private int overrides;
    private int revision;
    private bool stale = true;

    public ChannelStyleStore(Configuration configuration)
    {
        this.configuration = configuration;
        byIndex = new ChannelStyle?[GameChannels.All.Length];
    }

    public int Revision => revision;

    public bool HasOverrides
    {
        get
        {
            Sync();
            return overrides > 0;
        }
    }

    public static bool CanHideFromGameChat(GameChannel channel) => channel.Category != ChannelCategory.System;

    public bool Tracks(Configuration other) => ReferenceEquals(configuration, other);

    public ChannelStyle? For(string channelKey)
    {
        Sync();
        if (overrides == 0)
        {
            return null;
        }

        return GameChannels.TryByKey(channelKey, out var channel) ? byIndex[channel.Index] : null;
    }

    public bool IsCustomized(string channelKey) => For(channelKey) is not null;

    public bool NeverUnread(string channelKey) => For(channelKey) is { NeverUnread: true };

    public bool HidesOutgoing(string channelKey) => For(channelKey) is { HideOutgoing: true };

    public bool HidesFromGameChat(GameChannel channel) =>
        configuration.LinkpearlHideHandledFromGameChat && CanHideFromGameChat(channel) &&
        For(channel.Key) is { HideFromGameChat: true };

    public void Load(string channelKey, ChannelStyle into)
    {
        if (configuration.LinkpearlChannelStyles.TryGetValue(channelKey, out var stored) && stored is not null)
        {
            into.CopyFrom(stored);
            return;
        }

        into.Clear();
    }

    public void Apply(string channelKey, ChannelStyle style)
    {
        if (style.IsDefault)
        {
            Reset(channelKey);
            return;
        }

        if (!configuration.LinkpearlChannelStyles.TryGetValue(channelKey, out var stored) || stored is null)
        {
            stored = new ChannelStyle();
            configuration.LinkpearlChannelStyles[channelKey] = stored;
        }

        stored.CopyFrom(style);
        Touch();
    }

    public void Reset(string channelKey)
    {
        if (!configuration.LinkpearlChannelStyles.Remove(channelKey))
        {
            return;
        }

        Touch();
    }

    public void Invalidate() => Touch();

    private void Touch()
    {
        stale = true;
        revision++;
    }

    private void Sync()
    {
        if (!stale)
        {
            return;
        }

        stale = false;
        Array.Clear(byIndex);
        overrides = 0;
        var stored = configuration.LinkpearlChannelStyles;
        if (stored.Count == 0)
        {
            return;
        }

        foreach (var pair in stored)
        {
            if (pair.Value is null || pair.Value.IsDefault || !GameChannels.TryByKey(pair.Key, out var channel))
            {
                continue;
            }

            byIndex[channel.Index] = pair.Value;
            overrides++;
        }
    }
}

internal static class ChannelStyles
{
    private static ChannelStyleStore? shared;

    public static ChannelStyleStore Shared => shared ?? Bind(Plugin.Cfg);

    public static ChannelStyleStore Bind(Configuration configuration)
    {
        if (shared is null || !shared.Tracks(configuration))
        {
            shared = new ChannelStyleStore(configuration);
        }

        return shared;
    }
}
