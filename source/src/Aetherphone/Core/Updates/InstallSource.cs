using System.Reflection;
using Dalamud.Plugin;

namespace Aetherphone.Core.Updates;

internal static class InstallSource
{
    private const string DevRepository = "DEV";

    public static string Repository { get; private set; } = string.Empty;

    public static string Build { get; private set; } = AepConstants.Version;

    public static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        Repository = pluginInterface.IsDev ? DevRepository : pluginInterface.SourceRepository ?? string.Empty;
        var informational = typeof(InstallSource).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Build = string.IsNullOrEmpty(informational) ? AepConstants.Version : informational;
    }
}
