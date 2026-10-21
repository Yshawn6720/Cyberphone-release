namespace Aetherphone.Core;

internal static class AepConstants
{
    // Display name, slash commands, plugin file and InternalName are Cyberphone's own. The assembly name
    // stays "Aetherphone" until the SixLabors key for a renamed assembly is in hand (see NOTICE).
    #if DEBUG
    public const string Name = "CyberphoneDev";
    public const string ServerBarTag = "Dev";
    public const string PrimaryCommand = "/cyberphonedev";
    public const string AliasCommand = "/cphonedev";
    #elif BETA
    public const string Name = "CyberphoneBeta";
    public const string ServerBarTag = "Beta";
    public const string PrimaryCommand = "/cyberphonebeta";
    public const string AliasCommand = "/cphonebeta";
    #else
    public const string Name = "Cyberphone";
    public const string ServerBarTag = "";
    public const string PrimaryCommand = "/cyberphone";
    public const string AliasCommand = "/cphone";
    #endif

    // Every configuration ships Cyberphone.dll with this InternalName (see Aetherphone.csproj TargetName).
    public const string InternalName = "Cyberphone";

    #if DEBUG || BETA
    public static readonly bool IsPrerelease = true;
    #else
    public static readonly bool IsPrerelease = false;
    #endif

    #if BETA
    public static readonly bool IsBeta = true;
    #else
    public static readonly bool IsBeta = false;
    #endif
    public const string DiscordUrl = "https://discord.gg/3HbJCscMyS";
    public const string WebsiteUrl = "https://github.com/Yshawn6720/Cyberphone";
    public const string PatreonUrl = "https://www.patreon.com/c/xeldaralz";
    public const string BuyMeACoffeeUrl = "https://buymeacoffee.com/xeldaralz";
    public const string OfficialRepositoryUrl = "https://github.com/Yshawn6720/Cyberphone";
    public static readonly string Version = typeof(AepConstants).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
