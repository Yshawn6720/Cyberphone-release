namespace Aetherphone.Core.Apps;

internal static class AppVisits
{
    private static readonly Dictionary<string, int> StampByApp = new();
    private static int visitCounter;

    public static int Active { get; private set; }

    public static void NoteOpened(string appId) => StampByApp[appId] = ++visitCounter;

    public static Scope Enter(string appId)
    {
        var previous = Active;
        Active = StampByApp.TryGetValue(appId, out var stamp) ? stamp : 0;
        return new Scope(previous);
    }

    public ref struct Scope
    {
        private readonly int previous;

        internal Scope(int previous)
        {
            this.previous = previous;
        }

        public void Dispose() => Active = previous;
    }
}
