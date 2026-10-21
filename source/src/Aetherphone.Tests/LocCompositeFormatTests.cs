using System.Globalization;
using System.Text;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LocCompositeFormatTests
{
    private static readonly object[] Arguments = { 7, "Kupo", 1234567, 0.5f, 42, "Ul'dah" };

    [Fact]
    public void EveryCatalogTemplateFormatsTheSameThroughCompositeFormat()
    {
        var directory = LocalizationDirectory();
        var compared = 0;

        for (var languageIndex = 0; languageIndex < Languages.All.Length; languageIndex++)
        {
            var language = Languages.All[languageIndex];
            var culture = CultureOf(language.CultureName);
            var catalog = StringCatalog.Load(Path.Combine(directory, string.Concat(language.Code, ".json")));

            foreach (var key in catalog.Keys)
            {
                if (!catalog.TryGet(key, out var template))
                {
                    continue;
                }

                var highest = HighestPlaceholder(template);
                if (highest < 0 || highest >= Arguments.Length)
                {
                    continue;
                }

                var arguments = new object[highest + 1];
                Array.Copy(Arguments, arguments, arguments.Length);

                var byTemplate = string.Format(culture, template, arguments);
                var byComposite = string.Format(culture, CompositeFormat.Parse(template), arguments);

                Assert.Equal(byTemplate, byComposite);
                compared++;
            }
        }

        Assert.True(compared > 0, "No catalog template carried a placeholder, so nothing was compared.");
    }

    [Fact]
    public void EveryCatalogTemplateParsesAsACompositeFormat()
    {
        var directory = LocalizationDirectory();

        for (var languageIndex = 0; languageIndex < Languages.All.Length; languageIndex++)
        {
            var language = Languages.All[languageIndex];
            var catalog = StringCatalog.Load(Path.Combine(directory, string.Concat(language.Code, ".json")));

            foreach (var key in catalog.Keys)
            {
                if (!catalog.TryGet(key, out var template))
                {
                    continue;
                }

                var exception = Record.Exception(() => CompositeFormat.Parse(template));
                Assert.True(exception is null,
                    $"'{language.Code}.json' key '{key}' cannot be parsed as a composite format: {template}");
            }
        }
    }

    private static int HighestPlaceholder(string template)
    {
        var highest = -1;
        for (var index = 0; index < template.Length - 1; index++)
        {
            if (template[index] != '{')
            {
                continue;
            }

            var digit = template[index + 1];
            if (digit is >= '0' and <= '9')
            {
                var position = digit - '0';
                if (position > highest)
                {
                    highest = position;
                }
            }
        }

        return highest;
    }

    private static CultureInfo CultureOf(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
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
