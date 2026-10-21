using System.Text;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TraditionalChineseCatalogTests
{
    private const int ReportLimit = 20;

    private static readonly string[] MainlandTerms =
    {
        "視頻", "軟件", "網絡", "默認", "屏幕", "用戶", "登錄", "鼠標", "菜單", "賬", "質量", "信息", "帖子", "點贊",
        "會話", "退出登入", "許可權", "指令碼", "擴充套件", "全域性", "艾歐澤亞", "部隊", "以太", "莫古力", "金幣",
        "莫古影院", "“", "”",
    };

    private static readonly string[] KeptLatinTerms = { "MogCast", "Gil", "App" };

    [Fact]
    public void EveryIdeographIsInBig5SoTheTaiwanGameFontCoversIt()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var big5 = Encoding.GetEncoding(950, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var catalog = Load(Languages.TraditionalChinese);
        var offenders = new List<string>();
        var buffer = new char[1];
        foreach (var key in catalog.Keys)
        {
            catalog.TryGet(key, out var value);
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!IsIdeograph(character))
                {
                    continue;
                }

                buffer[0] = character;
                try
                {
                    big5.GetByteCount(buffer);
                }
                catch (EncoderFallbackException)
                {
                    offenders.Add($"{key}: '{character}'");
                }
            }
        }

        Assert.True(offenders.Count == 0, Describe("characters outside Big5 (simplified or uncovered)", offenders));
    }

    [Fact]
    public void EveryStringTheSimplifiedCatalogTranslatesIsTranslated()
    {
        var simplified = Load(Languages.Chinese);
        var traditional = Load(Languages.TraditionalChinese);
        var untranslated = new List<string>();
        foreach (var key in simplified.Keys)
        {
            simplified.TryGet(key, out var source);
            if (!ContainsIdeograph(source))
            {
                continue;
            }

            if (!traditional.TryGet(key, out var value) || (!ContainsIdeograph(value) && !OnlyKeptLatin(value)))
            {
                untranslated.Add(key);
            }
        }

        Assert.True(untranslated.Count == 0, Describe("keys left untranslated", untranslated));
    }

    [Fact]
    public void NoMainlandTermsOrQuotesRemain()
    {
        var catalog = Load(Languages.TraditionalChinese);
        var offenders = new List<string>();
        foreach (var key in catalog.Keys)
        {
            catalog.TryGet(key, out var value);
            for (var termIndex = 0; termIndex < MainlandTerms.Length; termIndex++)
            {
                if (value.Contains(MainlandTerms[termIndex], StringComparison.Ordinal))
                {
                    offenders.Add($"{key}: '{MainlandTerms[termIndex]}'");
                }
            }
        }

        Assert.True(offenders.Count == 0, Describe("mainland terms", offenders));
    }

    private static bool IsIdeograph(char character) => character is >= '\u4E00' and <= '\u9FFF';

    private static bool ContainsIdeograph(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (IsIdeograph(text[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool OnlyKeptLatin(string text)
    {
        var remainder = text;
        for (var termIndex = 0; termIndex < KeptLatinTerms.Length; termIndex++)
        {
            remainder = remainder.Replace(KeptLatinTerms[termIndex], string.Empty, StringComparison.Ordinal);
        }

        for (var index = 0; index < remainder.Length; index++)
        {
            if (char.IsLetter(remainder[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string Describe(string what, List<string> entries)
    {
        var builder = new StringBuilder();
        builder.Append(entries.Count).Append(' ').Append(what).Append(':');
        for (var index = 0; index < entries.Count && index < ReportLimit; index++)
        {
            builder.Append("\n  ").Append(entries[index]);
        }

        return builder.ToString();
    }

    private static StringCatalog Load(LanguageInfo language)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Aetherphone", "Localization");
            if (Directory.Exists(candidate))
            {
                var catalog = StringCatalog.Load(Path.Combine(candidate, string.Concat(language.Code, ".json")));
                Assert.True(catalog.Count > 0, $"'{language.Code}.json' is missing or empty.");
                return catalog;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate src/Aetherphone/Localization above '{AppContext.BaseDirectory}'.");
    }
}
