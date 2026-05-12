using System;

namespace RetroModemBridge;

public sealed class SshDialProfile
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public static bool TryParse(string dialTarget, out SshDialProfile profile, out string error)
    {
        profile = new SshDialProfile();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(dialTarget))
        {
            error = "SSH target is blank.";
            return false;
        }

        dialTarget = dialTarget.Trim();

        if (!dialTarget.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
        {
            error = "SSH target must start with ssh://";
            return false;
        }

        if (!Uri.TryCreate(dialTarget, UriKind.Absolute, out var uri))
        {
            error = "Invalid SSH URL.";
            return false;
        }

        if (!string.Equals(uri.Scheme, "ssh", StringComparison.OrdinalIgnoreCase))
        {
            error = "Invalid SSH scheme.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "SSH host is missing.";
            return false;
        }

        var userInfo = uri.UserInfo;
        if (string.IsNullOrWhiteSpace(userInfo))
        {
            error = "SSH username is missing. Use ssh://username@host:22";
            return false;
        }

        var username = Uri.UnescapeDataString(userInfo.Split(':')[0]);
        if (string.IsNullOrWhiteSpace(username))
        {
            error = "SSH username is missing.";
            return false;
        }

        var port = uri.IsDefaultPort ? 22 : uri.Port;
        if (port < 1 || port > 65535)
        {
            error = "SSH port must be between 1 and 65535.";
            return false;
        }

        profile = new SshDialProfile
        {
            Host = uri.Host,
            Port = port,
            Username = username,
            DisplayName = $"{username}@{uri.Host}:{port}"
        };

        return true;
    }
}
