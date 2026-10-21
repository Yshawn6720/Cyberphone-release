namespace Aetherphone.Core.Strats;

internal sealed class ResolvedMechanic
{
    public string Name = string.Empty;
    public GuideText? Description;
    public GuideText? Action;
    public GuideText? Notes;
    public ImageRef? Image;
    public string Transform = string.Empty;
    public GuideLink[] Links = Array.Empty<GuideLink>();
    public ArenaDiagram? Arena;
    public GuideText? PlayerText;
    public ImageRef? PlayerImage;
    public SpotlightMask? PlayerSpotlight;
    public string PlayerTransform = string.Empty;
    public bool PlayerIsFilterVariant;

    public bool HasContent =>
        Description is { IsEmpty: false } || Action is { IsEmpty: false } || Image is not null ||
        PlayerText is { IsEmpty: false } || PlayerImage is not null || Arena is not null;
}

internal sealed class ResolvedPhase
{
    public string Name = string.Empty;
    public string Tag = string.Empty;
    public GuideText? Description;
    public ImageRef? Image;
    public SpotlightMask? Spotlight;
    public string BoardUrl = string.Empty;
    public GuideLink[] Links = Array.Empty<GuideLink>();
    public ResolvedMechanic[] Mechs = Array.Empty<ResolvedMechanic>();
}

internal sealed class ResolvedFight
{
    public FightDoc Doc = null!;
    public StratVariant Strat = null!;
    public int StratIndex;
    public int TabIndex;
    public string Alignment = string.Empty;
    public int AlignmentIndex;
    public string[] ToggleValues = Array.Empty<string>();
    public int[] ToggleOptionIndices = Array.Empty<int>();
    public bool[] ToggleVisible = Array.Empty<bool>();
    public ResolvedPhase[] Phases = Array.Empty<ResolvedPhase>();
    public TimelineEntry[] Timeline = Array.Empty<TimelineEntry>();
    public int Revision;
}

internal static class StratsResolver
{
    public static ResolvedFight Build(FightDoc doc, StratsSelection selection)
    {
        var stratIndex = FindStrat(doc, selection.StratId);
        var strat = doc.Strats[stratIndex];
        var state = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in strat.Defaults)
        {
            state[pair.Key] = pair.Value;
        }

        var toggleValues = new string[doc.Toggles.Length];
        var toggleIndices = new int[doc.Toggles.Length];
        var toggleVisible = new bool[doc.Toggles.Length];
        var tab = doc.Tabs.Length > 0 ? doc.Tabs[Math.Clamp(selection.Tab, 0, doc.Tabs.Length - 1)] : null;
        var tabIndex = tab is null ? 0 : Math.Clamp(selection.Tab, 0, doc.Tabs.Length - 1);
        for (var index = 0; index < doc.Toggles.Length; index++)
        {
            var toggle = doc.Toggles[index];
            var value = ResolveToggleValue(toggle, selection, state);
            state[toggle.Key] = value;
            toggleValues[index] = value;
            toggleIndices[index] = OptionIndex(toggle, value);
            toggleVisible[index] = !toggle.IsMechFilter || tab is null || toggle.PhaseTag.Length == 0 ||
                Contains(tab.Tags, toggle.PhaseTag);
        }

        var alignmentIndex = FindAlignment(doc, selection.Alignment);
        var alignment = doc.Alignments.Length > 0 ? doc.Alignments[alignmentIndex].Id : string.Empty;
        var roleName = StratsRoles.RoleName(selection.Slot);
        var party = StratsRoles.Party(selection.Slot);
        if (doc.RoleOptions.Length > 0)
        {
            var option = doc.RoleOptions[Math.Clamp(selection.Slot, 0, doc.RoleOptions.Length - 1)];
            roleName = option.Role;
            party = option.Party;
        }

        var phases = new List<ResolvedPhase>(strat.Phases.Length);
        for (var index = 0; index < strat.Phases.Length; index++)
        {
            var phase = strat.Phases[index];
            if (tab is not null && phase.Tag.Length > 0 && !Contains(tab.Tags, phase.Tag))
            {
                continue;
            }

            phases.Add(ResolvePhase(phase, state, roleName, party, alignment, doc));
        }

        var timeline = doc.Timeline;
        if (doc.SplitTimeline && tab is not null)
        {
            var filtered = new List<TimelineEntry>(doc.Timeline.Length);
            for (var index = 0; index < doc.Timeline.Length; index++)
            {
                var item = doc.Timeline[index];
                if (item.Tag.Length == 0 || Contains(tab.Tags, item.Tag))
                {
                    filtered.Add(item);
                }
            }

            timeline = filtered.ToArray();
        }

        return new ResolvedFight
        {
            Doc = doc,
            Strat = strat,
            StratIndex = stratIndex,
            TabIndex = tabIndex,
            Alignment = alignment,
            AlignmentIndex = alignmentIndex,
            ToggleValues = toggleValues,
            ToggleOptionIndices = toggleIndices,
            ToggleVisible = toggleVisible,
            Phases = phases.ToArray(),
            Timeline = timeline,
            Revision = selection.Revision,
        };
    }

    private static int FindStrat(FightDoc doc, string stratId)
    {
        for (var index = 0; index < doc.Strats.Length; index++)
        {
            if (string.Equals(doc.Strats[index].Id, stratId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        for (var index = 0; index < doc.Strats.Length; index++)
        {
            if (string.Equals(doc.Strats[index].Id, doc.DefaultStrat, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private static int FindAlignment(FightDoc doc, string alignment)
    {
        for (var index = 0; index < doc.Alignments.Length; index++)
        {
            if (string.Equals(doc.Alignments[index].Id, alignment, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private static string ResolveToggleValue(ToggleDef toggle, StratsSelection selection,
        Dictionary<string, string> state)
    {
        if (selection.Toggles.TryGetValue(toggle.Key, out var chosen) && OptionIndex(toggle, chosen) >= 0)
        {
            return chosen;
        }

        if (state.TryGetValue(toggle.Key, out var stratDefault) && OptionIndex(toggle, stratDefault) >= 0)
        {
            return stratDefault;
        }

        return toggle.DefaultValue;
    }

    public static int OptionIndex(ToggleDef toggle, string value)
    {
        for (var index = 0; index < toggle.Options.Length; index++)
        {
            if (string.Equals(toggle.Options[index].Value, value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool Contains(string[] values, string value)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ResolvedPhase ResolvePhase(Phase phase, Dictionary<string, string> state, string roleName,
        int party, string alignment, FightDoc doc)
    {
        var mechs = Resolve(phase.Mechs, state) ?? Array.Empty<Mechanic>();
        var resolvedMechs = new List<ResolvedMechanic>(mechs.Length);
        for (var index = 0; index < mechs.Length; index++)
        {
            var mech = ResolveMechanic(mechs[index], state, roleName, party, alignment, doc);
            if (mech.HasContent)
            {
                resolvedMechs.Add(mech);
            }
        }

        return new ResolvedPhase
        {
            Name = phase.Name,
            Tag = phase.Tag,
            Description = NonEmpty(Resolve(phase.Description, state)),
            Image = Resolve(phase.Image, state),
            Spotlight = Resolve(phase.Spotlight, state),
            BoardUrl = Resolve(phase.BoardUrl, state) ?? string.Empty,
            Links = Resolve(phase.Links, state) ?? Array.Empty<GuideLink>(),
            Mechs = resolvedMechs.ToArray(),
        };
    }

    private static ResolvedMechanic ResolveMechanic(Mechanic mech, Dictionary<string, string> state,
        string roleName, int party, string alignment, FightDoc doc)
    {
        var resolved = new ResolvedMechanic
        {
            Name = mech.Name,
            Description = NonEmpty(Resolve(mech.Description, state)),
            Action = NonEmpty(mech.Action),
            Notes = NonEmpty(Resolve(mech.Notes, state)),
            Image = Resolve(mech.Image, state),
            Transform = Pick(mech.AlignmentTransforms, alignment, mech.Transform),
            Links = Resolve(mech.Links, state) ?? Array.Empty<GuideLink>(),
            Arena = mech.Arena,
        };

        var player = FindPlayer(mech.Players, state, roleName, party, doc);
        if (player is null)
        {
            return resolved;
        }

        resolved.PlayerText = NonEmpty(Resolve(player.Description, state));
        var image = Resolve(player.Image, state);
        if (alignment.Length > 0 && player.AlignmentImages.TryGetValue(alignment, out var alignedImage))
        {
            image = alignedImage;
        }

        resolved.PlayerImage = image;
        var spotlight = Resolve(player.Spotlight, state);
        if (alignment.Length > 0 && player.AlignmentSpotlights.TryGetValue(alignment, out var alignedSpotlight))
        {
            spotlight = alignedSpotlight;
        }

        resolved.PlayerSpotlight = spotlight;
        resolved.PlayerTransform = Pick(player.AlignmentTransforms, alignment, player.Transform);
        resolved.PlayerIsFilterVariant = player.ToggleKey.Length > 0 && IsMechFilter(doc, player.ToggleKey);
        return resolved;
    }

    private static bool IsMechFilter(FightDoc doc, string toggleKey)
    {
        for (var index = 0; index < doc.Toggles.Length; index++)
        {
            if (string.Equals(doc.Toggles[index].Key, toggleKey, StringComparison.Ordinal))
            {
                return doc.Toggles[index].IsMechFilter;
            }
        }

        return false;
    }

    private static PlayerEntry? FindPlayer(PlayerEntry[] players, Dictionary<string, string> state, string roleName,
        int party, FightDoc doc)
    {
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            if (player.Role.Length > 0 && !string.Equals(player.Role, roleName, StringComparison.Ordinal))
            {
                continue;
            }

            if (player.Party != 0 && player.Party != party)
            {
                continue;
            }

            if (player.ToggleKey.Length > 0)
            {
                var current = state.TryGetValue(player.ToggleKey, out var value) ? value : string.Empty;
                if (!string.Equals(player.ToggleValue, current, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            return player;
        }

        return null;
    }

    private static string Pick(Dictionary<string, string> byAlignment, string alignment, string fallback)
    {
        if (alignment.Length > 0 && byAlignment.TryGetValue(alignment, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static GuideText? NonEmpty(GuideText? text) => text is { IsEmpty: false } ? text : null;

    public static T? Resolve<T>(Variant<T>? variant, Dictionary<string, string> state) where T : class
    {
        if (variant is null)
        {
            return null;
        }

        if (variant.ByToggle is null)
        {
            return variant.One;
        }

        var values = variant.ByToggle.Values;
        var current = state.TryGetValue(variant.ByToggle.Key, out var value) ? value : string.Empty;
        if (values.TryGetValue(current, out var chosen))
        {
            return chosen;
        }

        foreach (var pair in values)
        {
            return pair.Value;
        }

        return variant.One;
    }
}
