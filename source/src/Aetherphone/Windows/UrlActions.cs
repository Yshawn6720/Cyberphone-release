using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;
using System.Diagnostics;

namespace Aetherphone.Windows;

internal static class UrlActions
{
    private const int DestinationMaxLength = 72;
    private const string Ellipsis = "…";
    private const string SchemeSeparator = "://";
    private const string BareHostPrefix = "www.";
    private const string DefaultScheme = "https://";
    private static readonly char[] PathStarts = ['/', '?', '#'];
    private static ConfirmService? confirm;

    public static void Configure(ConfirmService service)
    {
        confirm = service;
    }

    public static void OpenInBrowser(string rawUrl, Action<Exception>? onError = null)
    {
        var url = Normalize(rawUrl);
        if (!IsWebUrl(url) || Aetherphone.Core.Net.OfficialHostGuard.IsBlockedUrl(url))
        {
            var rejected = new NotSupportedException("Only http and https links can be opened.");
            AepLog.Warning(rejected, $"Refused to open a non-web link: {url}");
            onError?.Invoke(rejected);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"Opening {url} in a browser failed; copied it to the clipboard instead");
            ImGui.SetClipboardText(url);
            onError?.Invoke(exception);
        }
    }

    public static void AskThenOpen(string rawUrl, Action<Exception>? onError = null)
    {
        var url = Normalize(rawUrl);
        if (confirm is null)
        {
            OpenInBrowser(url, onError);
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Common.OpenLinkTitle),
            Message = string.Empty,
            Sections =
            [
                ConfirmSection.Paragraph(Loc.T(L.Common.OpenLinkWarning)),
                ConfirmSection.Chip(Loc.T(L.Common.OpenLinkDestination), DestinationLabel(url)),
            ],
            ConfirmLabel = Loc.T(L.Common.OpenLinkConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Confirm = () => OpenInBrowser(url, onError),
        });
    }

    public static string DestinationLabel(string url)
    {
        if (url.Length <= DestinationMaxLength)
        {
            return url;
        }

        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        var authorityStart = schemeEnd < 0 ? 0 : schemeEnd + 3;
        var authorityEnd = url.IndexOfAny(PathStarts, authorityStart);
        if (authorityEnd < 0)
        {
            authorityEnd = url.Length;
        }

        var keep = Math.Max(authorityEnd, DestinationMaxLength - Ellipsis.Length);
        if (keep >= url.Length)
        {
            return url;
        }

        return string.Concat(url.AsSpan(0, keep), Ellipsis);
    }

    public static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, $"Failed to open the folder: {path}");
        }
    }

    private static string Normalize(string url)
    {
        if (url.Contains(SchemeSeparator, StringComparison.Ordinal))
        {
            return url;
        }

        if (!url.StartsWith(BareHostPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return DefaultScheme + url;
    }

    private static bool IsWebUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return parsed.Host.Length > 0;
    }
}
