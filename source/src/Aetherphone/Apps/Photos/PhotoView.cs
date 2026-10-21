namespace Aetherphone.Apps.Photos;

internal enum PhotoRoute : byte
{
    Grid,
    Album,
    Viewer,
    AlbumPicker,
    AddToAlbum,
    Editor,
}

internal enum PhotosTab : byte
{
    Library,
    Collections,
}

internal enum LibraryLevel : byte
{
    Years,
    Months,
    Days,
    All,
}

internal readonly struct PhotoView
{
    public const int RecentsKey = -1;
    public const int FavoritesKey = -2;
    public const int TrashKey = -3;
    public const int FirstCustomAlbumId = 100;
    public const int PlaceKeyBase = -1_000_000;

    public readonly PhotoRoute Route;
    public readonly int AlbumKey;

    private PhotoView(PhotoRoute route, int albumKey)
    {
        Route = route;
        AlbumKey = albumKey;
    }

    public static PhotoView Grid() => new(PhotoRoute.Grid, 0);

    public static PhotoView Album(int albumKey) => new(PhotoRoute.Album, albumKey);

    public static PhotoView AlbumPicker(int albumKey) => new(PhotoRoute.AlbumPicker, albumKey);

    public static PhotoView Viewer() => new(PhotoRoute.Viewer, 0);

    public static PhotoView AddToAlbum() => new(PhotoRoute.AddToAlbum, 0);

    public static PhotoView Editor() => new(PhotoRoute.Editor, 0);

    public static int PlaceKey(uint territory) => PlaceKeyBase - (int)Math.Min(territory, int.MaxValue / 2);

    public static bool IsPlaceKey(int key) => key <= PlaceKeyBase;

    public static uint TerritoryOf(int placeKey) => (uint)(PlaceKeyBase - placeKey);

    public static bool IsCustomKey(int key) => key <= -FirstCustomAlbumId && key > PlaceKeyBase;

    public static bool IsMonthKey(int key) => key > 0;
}
