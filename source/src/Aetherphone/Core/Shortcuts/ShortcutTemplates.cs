using Aetherphone.Core.Localization;
using Dalamud.Interface;

namespace Aetherphone.Core.Shortcuts;

internal enum ShortcutTemplateGroup : byte
{
    Everyday,
    Party,
    Explore,
}

internal readonly record struct ShortcutTemplateStep(ShortcutStepKind Kind, string Text, float Seconds = 1f)
{
    public static ShortcutTemplateStep Command(string text) => new(ShortcutStepKind.Command, text);

    public static ShortcutTemplateStep Wait(float seconds) => new(ShortcutStepKind.Wait, string.Empty, seconds);

    public static ShortcutTemplateStep Link(string url) => new(ShortcutStepKind.OpenUrl, url);
}

internal sealed class ShortcutTemplate
{
    public ShortcutTemplate(string id, ShortcutTemplateGroup group, LocString name, FontAwesomeIcon glyph,
        string tint, params ShortcutTemplateStep[] steps)
    {
        Id = id;
        Group = group;
        Name = name;
        Glyph = glyph;
        Tint = tint;
        Steps = steps;
    }

    public string Id { get; }
    public ShortcutTemplateGroup Group { get; }
    public LocString Name { get; }
    public FontAwesomeIcon Glyph { get; }
    public string Tint { get; }
    public IReadOnlyList<ShortcutTemplateStep> Steps { get; }

    public ShortcutEntry Build()
    {
        var name = Loc.T(Name);
        var entry = new ShortcutEntry
        {
            Name = name.Length <= ShortcutStore.NameMaxLength ? name : name.Substring(0, ShortcutStore.NameMaxLength),
            Glyph = (int)Glyph,
            Tint = Tint,
        };

        for (var index = 0; index < Steps.Count; index++)
        {
            var step = Steps[index];
            entry.Steps.Add(new ShortcutStep { Kind = step.Kind, Text = step.Text, Seconds = step.Seconds });
        }

        return entry;
    }
}

internal static class ShortcutTemplates
{
    public static readonly ShortcutTemplateGroup[] Groups =
    {
        ShortcutTemplateGroup.Everyday, ShortcutTemplateGroup.Party, ShortcutTemplateGroup.Explore,
    };

    public static readonly ShortcutTemplate[] All =
    {
        new("photo", ShortcutTemplateGroup.Everyday, L.Shortcuts.TemplatePhotoMode, FontAwesomeIcon.Camera, "D16BEB",
            ShortcutTemplateStep.Command("/gpose")),
        new("sit", ShortcutTemplateGroup.Everyday, L.Shortcuts.TemplateSit, FontAwesomeIcon.Couch, "F58C38",
            ShortcutTemplateStep.Command("/groundsit")),
        new("wave", ShortcutTemplateGroup.Everyday, L.Shortcuts.TemplateWave, FontAwesomeIcon.HandPaper, "F0BD33",
            ShortcutTemplateStep.Command("/wave motion")),
        new("playtime", ShortcutTemplateGroup.Everyday, L.Shortcuts.TemplatePlayTime, FontAwesomeIcon.HourglassHalf,
            "858FA3", ShortcutTemplateStep.Command("/playtime")),
        new("readycheck", ShortcutTemplateGroup.Party, L.Shortcuts.TemplateReadyCheck, FontAwesomeIcon.CheckCircle,
            "3DC275", ShortcutTemplateStep.Command("/readycheck")),
        new("countdown", ShortcutTemplateGroup.Party, L.Shortcuts.TemplatePullTimer, FontAwesomeIcon.Clock,
            "F05C5C", ShortcutTemplateStep.Command("/countdown 15")),
        new("readypull", ShortcutTemplateGroup.Party, L.Shortcuts.TemplateReadyPull, FontAwesomeIcon.Bolt, "8C73F2",
            ShortcutTemplateStep.Command("/readycheck"), ShortcutTemplateStep.Wait(8f),
            ShortcutTemplateStep.Command("/countdown 10")),
        new("dutyfinder", ShortcutTemplateGroup.Party, L.Shortcuts.TemplateDutyFinder, FontAwesomeIcon.Dungeon,
            "5C8CFA", ShortcutTemplateStep.Command("/dutyfinder")),
        new("partyfinder", ShortcutTemplateGroup.Party, L.Shortcuts.TemplatePartyFinder, FontAwesomeIcon.Users,
            "42A8E6", ShortcutTemplateStep.Command("/partyfinder")),
        new("goldsaucer", ShortcutTemplateGroup.Explore, L.Shortcuts.TemplateGoldSaucer, FontAwesomeIcon.Dice,
            "F2619E", ShortcutTemplateStep.Command("/goldsaucer")),
        new("return", ShortcutTemplateGroup.Explore, L.Shortcuts.TemplateReturn, FontAwesomeIcon.Home, "2EC2B8",
            ShortcutTemplateStep.Command("/return")),
        new("lodestone", ShortcutTemplateGroup.Explore, L.Shortcuts.TemplateLodestone, FontAwesomeIcon.Globe,
            "9EC73D", ShortcutTemplateStep.Link("https://na.finalfantasyxiv.com/lodestone/")),
    };

    public static LocString GroupTitle(ShortcutTemplateGroup group) => group switch
    {
        ShortcutTemplateGroup.Party => L.Shortcuts.GalleryParty,
        ShortcutTemplateGroup.Explore => L.Shortcuts.GalleryExplore,
        _ => L.Shortcuts.GalleryEveryday,
    };
}
