using Aetherphone.Core.Localization;

namespace Aetherphone.Core;

internal static class DisplayVersion
{
    private const int BuildsPerRelease = 100;
    private const string NameSeparator = "  ";
    private static readonly Version AssemblyVersion = typeof(DisplayVersion).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
    private static readonly int BetaNumber = AssemblyVersion.Revision % BuildsPerRelease;
    private static readonly bool IsNumberedBeta = AepConstants.IsBeta && BetaNumber > 0;
    private static readonly string PreviewedVersion = new Version(AssemblyVersion.Major, AssemblyVersion.Minor,
        Math.Max(AssemblyVersion.Build, 0), Math.Max(AssemblyVersion.Revision, 0) / BuildsPerRelease).ToString();
    private static LanguageInfo? labelLanguage;
    private static string label = AepConstants.Version;
    private static string namedLabel = string.Concat(AepConstants.Name, NameSeparator, AepConstants.Version);

    public static string Text
    {
        get
        {
            Refresh();
            return label;
        }
    }

    public static string WithName
    {
        get
        {
            Refresh();
            return namedLabel;
        }
    }

    private static void Refresh()
    {
        if (!IsNumberedBeta || ReferenceEquals(labelLanguage, Loc.Current))
        {
            return;
        }

        labelLanguage = Loc.Current;
        label = Loc.T(L.Settings.BetaVersion, PreviewedVersion, BetaNumber);
        namedLabel = string.Concat(AepConstants.Name, NameSeparator, label);
    }
}
