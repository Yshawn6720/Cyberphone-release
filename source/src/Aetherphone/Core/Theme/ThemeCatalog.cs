namespace Aetherphone.Core.Theme;

internal sealed record NamedColor(string Name, Vector4 Color);

internal static class ThemeCatalog
{
    public static readonly IReadOnlyList<NamedColor> Accents = new NamedColor[]
    {
        new("Violet", new Vector4(0.55f, 0.45f, 0.95f, 1f)), new("Blue", new Vector4(0.30f, 0.55f, 0.98f, 1f)),
        new("Green", new Vector4(0.20f, 0.78f, 0.45f, 1f)), new("Pink", new Vector4(0.95f, 0.40f, 0.65f, 1f)),
        new("Amber", new Vector4(0.96f, 0.65f, 0.20f, 1f)),
    };

    public const string DefaultCaseName = "Titanium";

    // Upstream's art cases are contributed artwork that does not transfer with a fork (TRADEMARK.md), so
    // Cyberphone ships only the code-drawn case. Saved configs that name a removed case resolve to it.
    private static readonly PhoneCase[] BuiltInCases =
    {
        PhoneCase.Color(DefaultCaseName, new Vector4(0.145f, 0.145f, 0.170f, 1f)),
    };

    public static IReadOnlyList<PhoneCase> Cases { get; } = BuiltInCases;

    public static bool IsCustomAccent(string name) => name.Length > 0 && name[0] == '#';

    public static Vector4 ResolveAccent(string name) =>
        IsCustomAccent(name) && HexColor.TryParse(name, out var custom)
            ? custom
            : Accents[IndexOf(Accents, name)].Color;

    public static PhoneCase ResolveCase(string id) => Cases[IndexOf(Cases, id)];

    public static int IndexOf(IReadOnlyList<NamedColor> list, string name)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index].Name == name)
            {
                return index;
            }
        }

        return 0;
    }

    public static int IndexOf(IReadOnlyList<PhoneCase> list, string id)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index].Id == id)
            {
                return index;
            }
        }

        return 0;
    }
}
