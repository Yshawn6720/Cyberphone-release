using System.Reflection;
using System.Text;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LocalizationParityTests
{
    private const int ReportLimit = 20;

    [Fact]
    public void EveryCatalogCarriesExactlyTheKeysDeclaredInL()
    {
        var declared = CollectDeclaredKeys();
        var directory = LocalizationDirectory();

        Assert.True(declared.Count > 0, "No LocString keys were discovered on L.");

        for (var index = 0; index < Languages.All.Length; index++)
        {
            var code = Languages.All[index].Code;
            var catalog = StringCatalog.Load(Path.Combine(directory, string.Concat(code, ".json")));

            Assert.True(catalog.Count > 0, $"'{code}.json' is missing, empty, or failed to parse.");

            var missing = Missing(declared, catalog);
            Assert.True(missing.Count == 0,
                $"'{code}.json' is missing {missing.Count} key(s) declared in L.cs: {Describe(missing)}");

            var orphaned = Orphaned(declared, catalog);
            Assert.True(orphaned.Count == 0,
                $"'{code}.json' declares {orphaned.Count} key(s) with no LocString in L.cs: {Describe(orphaned)}");
        }
    }

    private static List<string> Missing(HashSet<string> declared, StringCatalog catalog)
    {
        var missing = new List<string>();
        foreach (var key in declared)
        {
            if (!catalog.Contains(key))
            {
                missing.Add(key);
            }
        }

        missing.Sort(StringComparer.Ordinal);
        return missing;
    }

    private static List<string> Orphaned(HashSet<string> declared, StringCatalog catalog)
    {
        var orphaned = new List<string>();
        foreach (var key in catalog.Keys)
        {
            if (!declared.Contains(key))
            {
                orphaned.Add(key);
            }
        }

        orphaned.Sort(StringComparer.Ordinal);
        return orphaned;
    }

    private static string Describe(List<string> keys)
    {
        var builder = new StringBuilder();
        var shown = keys.Count < ReportLimit ? keys.Count : ReportLimit;
        for (var index = 0; index < shown; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(keys[index]);
        }

        if (keys.Count > shown)
        {
            builder.Append(", and ").Append(keys.Count - shown).Append(" more");
        }

        return builder.ToString();
    }

    private static HashSet<string> CollectDeclaredKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        Collect(typeof(L), keys);
        return keys;
    }

    private static void Collect(Type type, HashSet<string> keys)
    {
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
        {
            var field = fields[fieldIndex];
            if (field.FieldType == typeof(LocString))
            {
                keys.Add(((LocString)field.GetValue(null)!).Key);
                continue;
            }

            if (field.FieldType == typeof(LocPlural))
            {
                var keyBase = ((LocPlural)field.GetValue(null)!).KeyBase;
                keys.Add(string.Concat(keyBase, ".one"));
                keys.Add(string.Concat(keyBase, ".other"));
                continue;
            }

            if (field.FieldType != typeof(LocString[]))
            {
                continue;
            }

            var entries = (LocString[])field.GetValue(null)!;
            for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                keys.Add(entries[entryIndex].Key);
            }
        }

        var nested = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
        for (var typeIndex = 0; typeIndex < nested.Length; typeIndex++)
        {
            Collect(nested[typeIndex], keys);
        }
    }

    private static string LocalizationDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Aetherphone", "Localization");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate src/Aetherphone/Localization above '{AppContext.BaseDirectory}'.");
    }
}
