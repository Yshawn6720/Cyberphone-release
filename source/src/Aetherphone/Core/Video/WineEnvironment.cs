using System.Runtime.InteropServices;

namespace Aetherphone.Core.Video;

internal static class WineEnvironment
{
    private static readonly Lazy<bool> IsWineLazy = new(Detect);

    public static bool IsWine => IsWineLazy.Value;

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern nint GetModuleHandle(string moduleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern nint GetProcAddress(nint module, string procName);

    private static bool Detect()
    {
        var ntdll = GetModuleHandle("ntdll.dll");
        return ntdll != nint.Zero && GetProcAddress(ntdll, "wine_get_version") != nint.Zero;
    }
}
