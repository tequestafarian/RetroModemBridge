using System.Diagnostics;
using System.Net.Sockets;

namespace RetroModemBridge;

public sealed class ConnectionTestResult
{
    public string Result { get; init; } = string.Empty;
    public int? ResponseMs { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.Now;
}

public static class ConnectionTester
{
    public static async Task<string> TestAsync(string host, int port, int timeoutMs = 5000)
    {
        var detail = await TestDetailedAsync(host, port, timeoutMs).ConfigureAwait(false);
        return detail.Result;
    }

    public static async Task<ConnectionTestResult> TestDetailedAsync(string host, int port, int timeoutMs = 5000)
    {
        var checkedAt = DateTime.Now;

        if (string.IsNullOrWhiteSpace(host))
            return new ConnectionTestResult { Result = "Missing host", CheckedAt = checkedAt };

        if (port < 1 || port > 65535)
            return new ConnectionTestResult { Result = "Invalid port", CheckedAt = checkedAt };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host.Trim(), port);
            var timeoutTask = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

            if (completed == timeoutTask)
                return new ConnectionTestResult { Result = "Timed out", ResponseMs = timeoutMs, CheckedAt = checkedAt };

            await connectTask.ConfigureAwait(false);
            stopwatch.Stop();

            return new ConnectionTestResult
            {
                Result = client.Connected ? "Online" : "Failed",
                ResponseMs = (int)Math.Max(0, stopwatch.ElapsedMilliseconds),
                CheckedAt = checkedAt
            };
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();

            var result = ex.SocketErrorCode switch
            {
                SocketError.HostNotFound => "DNS failed",
                SocketError.ConnectionRefused => "Port refused",
                SocketError.TimedOut => "Timed out",
                SocketError.NetworkUnreachable => "Network unreachable",
                _ => "Socket error: " + ex.SocketErrorCode
            };

            return new ConnectionTestResult
            {
                Result = result,
                ResponseMs = (int)Math.Max(0, stopwatch.ElapsedMilliseconds),
                CheckedAt = checkedAt
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new ConnectionTestResult
            {
                Result = "Failed: " + ex.Message,
                ResponseMs = (int)Math.Max(0, stopwatch.ElapsedMilliseconds),
                CheckedAt = checkedAt
            };
        }
    }
}
