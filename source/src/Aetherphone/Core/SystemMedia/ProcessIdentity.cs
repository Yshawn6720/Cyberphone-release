using System.Runtime.InteropServices;

namespace Aetherphone.Core.SystemMedia;

internal static unsafe class ProcessIdentity
{
    private const uint QueryLimitedInformation = 0x1000;
    private const int MaximumPath = 1024;
    private const int MaximumPackageFamilyName = 128;

    public static bool Matches(uint processId, string appUserModelId)
    {
        if (processId == 0)
        {
            return false;
        }

        var process = OpenProcess(QueryLimitedInformation, 0, processId);
        if (process == 0)
        {
            return false;
        }

        try
        {
            var familyBuffer = stackalloc char[MaximumPackageFamilyName];
            var familyLength = (uint)MaximumPackageFamilyName;
            var family = GetPackageFamilyName(process, &familyLength, familyBuffer) == 0 && familyLength > 0
                ? new ReadOnlySpan<char>(familyBuffer, (int)familyLength - 1)
                : ReadOnlySpan<char>.Empty;
            var pathBuffer = stackalloc char[MaximumPath];
            var pathLength = (uint)MaximumPath;
            var path = QueryFullProcessImageNameW(process, 0, pathBuffer, &pathLength) != 0
                ? new ReadOnlySpan<char>(pathBuffer, (int)pathLength)
                : ReadOnlySpan<char>.Empty;
            return MediaAppNames.MatchesProcess(appUserModelId, family, path);
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern nint OpenProcess(uint access, int inheritHandle, uint processId);

    [DllImport("kernel32.dll")]
    private static extern int CloseHandle(nint handle);

    [DllImport("kernel32.dll")]
    private static extern int QueryFullProcessImageNameW(nint process, uint flags, char* name, uint* size);

    [DllImport("kernel32.dll")]
    private static extern int GetPackageFamilyName(nint process, uint* length, char* name);
}
