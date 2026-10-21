namespace Aetherphone.Core;

internal static class AepLog
{
    public static void Verbose(string message) => Plugin.Log?.Verbose(message);

    public static void Debug(string message) => Plugin.Log?.Debug(message);

    public static void Debug(Exception exception, string message) => Plugin.Log?.Debug(exception, message);

    public static void Info(string message) => Plugin.Log?.Information(message);

    public static void Info(Exception exception, string message) => Plugin.Log?.Information(exception, message);

    public static void Warning(string message) => Plugin.Log?.Warning(message);

    public static void Warning(Exception exception, string message) => Plugin.Log?.Warning(exception, message);

    public static void Error(string message) => Plugin.Log?.Error(message);

    public static void Error(Exception exception, string message) => Plugin.Log?.Error(exception, message);
}
