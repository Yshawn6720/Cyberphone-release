namespace Aetherphone.Core.Theme;

internal enum PhoneCaseKind : byte
{
    Color,
    Art,
}

internal enum PhoneCaseCategory : byte
{
    Colors,
    Gradients,
    ArtistSeries,
}

internal sealed record PhoneCase(string Id, PhoneCaseKind Kind, PhoneCaseCategory Category, Vector4 Tint,
    string TextureId, string ArtistName, string ArtistUrl)
{
    public bool HasArtist => ArtistName.Length > 0;

    public static PhoneCase Color(string id, Vector4 tint) =>
        new(id, PhoneCaseKind.Color, PhoneCaseCategory.Colors, tint, string.Empty, string.Empty, string.Empty);

    public static PhoneCase Art(string id, PhoneCaseCategory category, Vector4 tint, string artistName = "",
        string artistUrl = "") =>
        new(id, PhoneCaseKind.Art, category, tint, id, artistName, artistUrl);
}
