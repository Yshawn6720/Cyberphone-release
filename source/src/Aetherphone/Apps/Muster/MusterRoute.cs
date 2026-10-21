namespace Aetherphone.Apps.Muster;

internal enum MusterScreen : byte
{
    Root,
    Detail,
    Create,
    Manage,
    DataCenter,
}

internal readonly record struct MusterRoute(MusterScreen Screen, string? MusterId = null, string BackTitle = "")
{
    public static readonly MusterRoute Root = new(MusterScreen.Root);

    public static MusterRoute Detail(string musterId, string backTitle) =>
        new(MusterScreen.Detail, musterId, backTitle);

    public static MusterRoute Create(string backTitle) => new(MusterScreen.Create, null, backTitle);

    public static MusterRoute Manage(string backTitle) => new(MusterScreen.Manage, null, backTitle);

    public static MusterRoute DataCenter(string backTitle) => new(MusterScreen.DataCenter, null, backTitle);
}
