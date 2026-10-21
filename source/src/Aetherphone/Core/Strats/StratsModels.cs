using System.Text.Json.Serialization;

namespace Aetherphone.Core.Strats;

internal sealed class GuideRun
{
    [JsonPropertyName("t")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("b")]
    public bool Bold { get; set; }

    [JsonPropertyName("c")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;
}

internal sealed class GuidePara
{
    [JsonPropertyName("bullet")]
    public bool Bullet { get; set; }

    [JsonPropertyName("indent")]
    public int Indent { get; set; }

    [JsonPropertyName("runs")]
    public GuideRun[] Runs { get; set; } = Array.Empty<GuideRun>();
}

internal sealed class GuideText
{
    public static readonly GuideText Empty = new();

    [JsonPropertyName("paras")]
    public GuidePara[] Paras { get; set; } = Array.Empty<GuidePara>();

    public bool IsEmpty => Paras.Length == 0;
}

internal sealed class ImageRef
{
    [JsonPropertyName("k")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("w")]
    public int Width { get; set; }

    [JsonPropertyName("h")]
    public int Height { get; set; }
}

internal sealed class SpotlightCircle
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("r")]
    public float Radius { get; set; }
}

internal sealed class SpotlightRect
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("w")]
    public float Width { get; set; }

    [JsonPropertyName("h")]
    public float Height { get; set; }
}

internal sealed class SpotlightMask
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("circles")]
    public SpotlightCircle[] Circles { get; set; } = Array.Empty<SpotlightCircle>();

    [JsonPropertyName("rect")]
    public SpotlightRect? Rect { get; set; }
}

internal sealed class GuideLink
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

internal sealed class ByToggle<T>
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("values")]
    public Dictionary<string, T?> Values { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class Variant<T>
{
    [JsonPropertyName("one")]
    public T? One { get; set; }

    [JsonPropertyName("byToggle")]
    public ByToggle<T>? ByToggle { get; set; }
}

internal sealed class GuideBadge
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;
}

internal sealed class ToggleOption
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("badges")]
    public GuideBadge[] Badges { get; set; } = Array.Empty<GuideBadge>();

    [JsonPropertyName("links")]
    public GuideLink[] Links { get; set; } = Array.Empty<GuideLink>();
}

internal sealed class ToggleDef
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("defaultValue")]
    public string DefaultValue { get; set; } = string.Empty;

    [JsonPropertyName("isMechFilter")]
    public bool IsMechFilter { get; set; }

    [JsonPropertyName("phaseTag")]
    public string PhaseTag { get; set; } = string.Empty;

    [JsonPropertyName("options")]
    public ToggleOption[] Options { get; set; } = Array.Empty<ToggleOption>();
}

internal sealed class TabDef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [JsonPropertyName("inProgress")]
    public bool InProgress { get; set; }
}

internal sealed class RoleOption
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("abbrev")]
    public string Abbrev { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("party")]
    public int Party { get; set; }
}

internal sealed class AlignmentDef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

internal sealed class TimelineEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("startMs")]
    public int StartMs { get; set; }
}

internal sealed class StratDifference
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public GuideText Text { get; set; } = GuideText.Empty;

    [JsonPropertyName("tab")]
    public string Tab { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

internal sealed class ResourcesDef
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public GuideText Text { get; set; } = GuideText.Empty;

    [JsonPropertyName("links")]
    public GuideLink[] Links { get; set; } = Array.Empty<GuideLink>();
}

internal sealed class ArenaElement
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("x2")]
    public float X2 { get; set; }

    [JsonPropertyName("y2")]
    public float Y2 { get; set; }

    [JsonPropertyName("w")]
    public float Width { get; set; }

    [JsonPropertyName("h")]
    public float Height { get; set; }

    [JsonPropertyName("r")]
    public float Radius { get; set; }

    [JsonPropertyName("rotation")]
    public float Rotation { get; set; }

    [JsonPropertyName("size")]
    public float Size { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("opacity")]
    public float Opacity { get; set; } = 1f;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("job")]
    public string Job { get; set; } = string.Empty;

    [JsonPropertyName("marker")]
    public string Marker { get; set; } = string.Empty;

    [JsonPropertyName("mark")]
    public string Mark { get; set; } = string.Empty;

    [JsonPropertyName("shape")]
    public string Shape { get; set; } = string.Empty;

    [JsonPropertyName("dashed")]
    public bool Dashed { get; set; }
}

internal sealed class ArenaDiagram
{
    [JsonPropertyName("shape")]
    public string Shape { get; set; } = string.Empty;

    [JsonPropertyName("bg")]
    public string Background { get; set; } = string.Empty;

    [JsonPropertyName("elements")]
    public ArenaElement[] Elements { get; set; } = Array.Empty<ArenaElement>();

    [JsonPropertyName("fallbackUrl")]
    public string FallbackUrl { get; set; } = string.Empty;
}

internal sealed class PlayerEntry
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("party")]
    public int Party { get; set; }

    [JsonPropertyName("toggleKey")]
    public string ToggleKey { get; set; } = string.Empty;

    [JsonPropertyName("toggleValue")]
    public string ToggleValue { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public Variant<GuideText>? Description { get; set; }

    [JsonPropertyName("image")]
    public Variant<ImageRef>? Image { get; set; }

    [JsonPropertyName("spotlight")]
    public Variant<SpotlightMask>? Spotlight { get; set; }

    [JsonPropertyName("transform")]
    public string Transform { get; set; } = string.Empty;

    [JsonPropertyName("alignmentTransforms")]
    public Dictionary<string, string> AlignmentTransforms { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("alignmentImages")]
    public Dictionary<string, ImageRef> AlignmentImages { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("alignmentSpotlights")]
    public Dictionary<string, SpotlightMask> AlignmentSpotlights { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class Mechanic
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public Variant<GuideText>? Description { get; set; }

    [JsonPropertyName("action")]
    public GuideText? Action { get; set; }

    [JsonPropertyName("notes")]
    public Variant<GuideText>? Notes { get; set; }

    [JsonPropertyName("image")]
    public Variant<ImageRef>? Image { get; set; }

    [JsonPropertyName("spotlight")]
    public Variant<SpotlightMask>? Spotlight { get; set; }

    [JsonPropertyName("links")]
    public Variant<GuideLink[]>? Links { get; set; }

    [JsonPropertyName("transform")]
    public string Transform { get; set; } = string.Empty;

    [JsonPropertyName("alignmentTransforms")]
    public Dictionary<string, string> AlignmentTransforms { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("arena")]
    public ArenaDiagram? Arena { get; set; }

    [JsonPropertyName("players")]
    public PlayerEntry[] Players { get; set; } = Array.Empty<PlayerEntry>();
}

internal sealed class Phase
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public Variant<GuideText>? Description { get; set; }

    [JsonPropertyName("image")]
    public Variant<ImageRef>? Image { get; set; }

    [JsonPropertyName("spotlight")]
    public Variant<SpotlightMask>? Spotlight { get; set; }

    [JsonPropertyName("boardUrl")]
    public Variant<string>? BoardUrl { get; set; }

    [JsonPropertyName("links")]
    public Variant<GuideLink[]>? Links { get; set; }

    [JsonPropertyName("mechs")]
    public Variant<Mechanic[]>? Mechs { get; set; }
}

internal sealed class StratVariant
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("badges")]
    public GuideBadge[] Badges { get; set; } = Array.Empty<GuideBadge>();

    [JsonPropertyName("jpRoles")]
    public bool JpRoles { get; set; }

    [JsonPropertyName("defaults")]
    public Dictionary<string, string> Defaults { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("links")]
    public GuideLink[] Links { get; set; } = Array.Empty<GuideLink>();

    [JsonPropertyName("description")]
    public GuideText Description { get; set; } = GuideText.Empty;

    [JsonPropertyName("notes")]
    public GuideText Notes { get; set; } = GuideText.Empty;

    [JsonPropertyName("phases")]
    public Phase[] Phases { get; set; } = Array.Empty<Phase>();
}

internal sealed class FightDoc
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("fightKey")]
    public string FightKey { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("abbrev")]
    public string Abbrev { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonPropertyName("strats")]
    public StratVariant[] Strats { get; set; } = Array.Empty<StratVariant>();

    [JsonPropertyName("defaultStrat")]
    public string DefaultStrat { get; set; } = string.Empty;

    [JsonPropertyName("toggles")]
    public ToggleDef[] Toggles { get; set; } = Array.Empty<ToggleDef>();

    [JsonPropertyName("tabs")]
    public TabDef[] Tabs { get; set; } = Array.Empty<TabDef>();

    [JsonPropertyName("roleOptions")]
    public RoleOption[] RoleOptions { get; set; } = Array.Empty<RoleOption>();

    [JsonPropertyName("alignments")]
    public AlignmentDef[] Alignments { get; set; } = Array.Empty<AlignmentDef>();

    [JsonPropertyName("timeline")]
    public TimelineEntry[] Timeline { get; set; } = Array.Empty<TimelineEntry>();

    [JsonPropertyName("splitTimeline")]
    public bool SplitTimeline { get; set; }

    [JsonPropertyName("evenTimelineSpacing")]
    public bool EvenTimelineSpacing { get; set; }

    [JsonPropertyName("stratDifferences")]
    public StratDifference[] StratDifferences { get; set; } = Array.Empty<StratDifference>();

    [JsonPropertyName("resources")]
    public ResourcesDef? Resources { get; set; }

    [JsonPropertyName("separateDescriptionAction")]
    public bool SeparateDescriptionAction { get; set; }
}

internal sealed class ManifestFight
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("abbrev")]
    public string Abbrev { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonPropertyName("guideKey")]
    public string GuideKey { get; set; } = string.Empty;

    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = string.Empty;

    [JsonPropertyName("bytes")]
    public int Bytes { get; set; }

    [JsonPropertyName("territoryIds")]
    public uint[] TerritoryIds { get; set; } = Array.Empty<uint>();

    [JsonPropertyName("contentFinderIds")]
    public uint[] ContentFinderIds { get; set; } = Array.Empty<uint>();

    [JsonPropertyName("inProgress")]
    public bool InProgress { get; set; }
}

internal sealed class ManifestGroup
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("fights")]
    public ManifestFight[] Fights { get; set; } = Array.Empty<ManifestFight>();
}

internal sealed class ManifestCredits
{
    [JsonPropertyName("siteName")]
    public string SiteName { get; set; } = string.Empty;

    [JsonPropertyName("siteUrl")]
    public string SiteUrl { get; set; } = string.Empty;

    [JsonPropertyName("license")]
    public string License { get; set; } = string.Empty;

    [JsonPropertyName("licenseUrl")]
    public string LicenseUrl { get; set; } = string.Empty;

    [JsonPropertyName("links")]
    public GuideLink[] Links { get; set; } = Array.Empty<GuideLink>();
}

internal sealed class StratsManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("generatedAtUtc")]
    public string GeneratedAtUtc { get; set; } = string.Empty;

    [JsonPropertyName("sourceCommit")]
    public string SourceCommit { get; set; } = string.Empty;

    [JsonPropertyName("credits")]
    public ManifestCredits Credits { get; set; } = new();

    [JsonPropertyName("groups")]
    public ManifestGroup[] Groups { get; set; } = Array.Empty<ManifestGroup>();
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(StratsManifest))]
[JsonSerializable(typeof(FightDoc))]
internal sealed partial class StratsJsonContext : JsonSerializerContext;
