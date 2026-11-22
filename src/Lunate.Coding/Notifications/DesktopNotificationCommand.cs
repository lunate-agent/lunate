namespace Lunate.Coding;

/// <summary>The title and body of one desktop notification.</summary>
internal sealed record NotificationMessage(string Title, string Body);

/// <summary>The platform family whose notification tool applies.</summary>
internal enum NotificationPlatform
{
    MacOs,
    Linux,
    Windows,
    Unsupported,
}

/// <summary>
/// One notification process. The title and body travel separately from the executable and its
/// arguments: macOS and Windows read them from environment variables, Linux receives them after an
/// end-of-options marker, so no text is ever interpolated into a shell command.
/// </summary>
internal sealed record NotificationCommand(
    string Executable,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment
);

/// <summary>
/// The platform transport decision: which command shows a notification on the host, with the text
/// passed out of band. A platform with no supported transport yields null and the caller is silent.
/// </summary>
internal static class DesktopNotificationCommand
{
    internal const string TitleVariable = "LUNATE_NOTIFY_TITLE";
    internal const string BodyVariable = "LUNATE_NOTIFY_BODY";

    private const string MacScript =
        "display notification (system attribute \"LUNATE_NOTIFY_BODY\") with title (system attribute \"LUNATE_NOTIFY_TITLE\")";

    private const string WindowsScript =
        "$ErrorActionPreference='Stop';"
        + "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime] > $null;"
        + "$template=[Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02);"
        + "$texts=$template.GetElementsByTagName('text');"
        + "$texts.Item(0).AppendChild($template.CreateTextNode($env:LUNATE_NOTIFY_TITLE)) > $null;"
        + "$texts.Item(1).AppendChild($template.CreateTextNode($env:LUNATE_NOTIFY_BODY)) > $null;"
        + "$toast=[Windows.UI.Notifications.ToastNotification]::new($template);"
        + "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Lunate').Show($toast)";

    /// <summary>The host platform, with test seams for the two checks that choose a family.</summary>
    internal static NotificationPlatform Detect(
        Func<bool>? isWindows = null,
        Func<bool>? isMacOs = null,
        Func<bool>? isLinux = null
    )
    {
        if ((isWindows ?? OperatingSystem.IsWindows)())
        {
            return NotificationPlatform.Windows;
        }

        if ((isMacOs ?? OperatingSystem.IsMacOS)())
        {
            return NotificationPlatform.MacOs;
        }

        return (isLinux ?? OperatingSystem.IsLinux)()
            ? NotificationPlatform.Linux
            : NotificationPlatform.Unsupported;
    }

    internal static NotificationCommand? For(
        NotificationPlatform platform,
        NotificationMessage message
    )
    {
        ArgumentNullException.ThrowIfNull(message);
        IReadOnlyDictionary<string, string> text = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            [TitleVariable] = message.Title,
            [BodyVariable] = message.Body,
        };

        return platform switch
        {
            NotificationPlatform.MacOs => new NotificationCommand(
                "osascript",
                ["-e", MacScript],
                text
            ),
            NotificationPlatform.Linux => new NotificationCommand(
                "notify-send",
                ["--app-name=Lunate", "--urgency=normal", "--", message.Title, message.Body],
                new Dictionary<string, string>(StringComparer.Ordinal)
            ),
            NotificationPlatform.Windows => new NotificationCommand(
                "powershell.exe",
                [
                    "-NoProfile",
                    "-NonInteractive",
                    "-WindowStyle",
                    "Hidden",
                    "-Command",
                    WindowsScript,
                ],
                text
            ),
            _ => null,
        };
    }
}
