namespace Aetherphone.Core.Confirm;

internal static class ConfirmHosts
{
    public const int Phone = 0;

    private static int reserved = Phone;

    public static int Current { get; private set; } = Phone;

    public static int Reserve()
    {
        reserved++;
        return reserved;
    }

    public static Scope Enter(int host) => new(host);

    internal readonly ref struct Scope
    {
        private readonly int previous;

        public Scope(int host)
        {
            previous = Current;
            Current = host;
        }

        public void Dispose()
        {
            Current = previous;
        }
    }
}
