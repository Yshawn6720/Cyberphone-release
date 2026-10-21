using Newtonsoft.Json.Linq;

namespace Aetherphone.Core.Localization;

internal sealed class StringCatalog
{
    public static readonly StringCatalog Empty = new(new Dictionary<string, string>(0, StringComparer.Ordinal));
    private readonly Dictionary<string, string> entries;

    private StringCatalog(Dictionary<string, string> entries)
    {
        this.entries = entries;
    }

    public int Count => entries.Count;
    public IReadOnlyCollection<string> Keys => entries.Keys;
    public bool TryGet(string key, out string value) => entries.TryGetValue(key, out value!);
    public bool Contains(string key) => entries.ContainsKey(key);

    public static StringCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var root = JObject.Parse(File.ReadAllText(path));
            Flatten(root, null, entries);
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, $"Failed to load language catalog '{path}'");
            return Empty;
        }

        return new StringCatalog(entries);
    }

    public static ushort[] ScanGlyphs(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<ushort>();
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, $"Failed to scan glyphs from '{path}'");
            return Array.Empty<ushort>();
        }

        var coverage = new GlyphCoverage();
        for (var index = 0; index < text.Length; index++)
        {
            var codepoint = text[index];
            if (codepoint < GlyphPlan.FirstSharedCodepoint || char.IsSurrogate(codepoint))
            {
                continue;
            }

            coverage.Add(codepoint);
        }

        var glyphs = new ushort[coverage.Count];
        var offset = 0;
        for (var codepoint = GlyphPlan.FirstSharedCodepoint; codepoint <= char.MaxValue; codepoint++)
        {
            if (!coverage.Contains(codepoint))
            {
                continue;
            }

            glyphs[offset++] = (ushort)codepoint;
        }

        return glyphs;
    }

    private static void Flatten(JObject node, string? prefix, Dictionary<string, string> target)
    {
        foreach (var property in node.Properties())
        {
            var key = prefix is null ? property.Name : string.Concat(prefix, ".", property.Name);
            if (property.Value is JObject child)
            {
                Flatten(child, key, target);
                continue;
            }

            target[key] = property.Value.ToString();
        }
    }
}
