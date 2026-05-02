using System.Net.Sockets;

namespace RetroModemBridge;

public static class ConnectionTester
{
    public static async Task<string> TestAsync(string host, int port, int timeoutMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(host))
            return "Missing host";

        if (port < 1 || port > 65535)
            return "Invalid port";

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host.Trim(), port);
            var timeoutTask = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

            if (completed == timeoutTask)
                return "Timed out";

            await connectTask.ConfigureAwait(false);
            return client.Connected ? "Online" : "Failed";
        }
        catch (SocketException ex)
        {
            return ex.SocketErrorCode switch
            {
                SocketError.HostNotFound => "DNS failed",
                SocketError.ConnectionRefused => "Port refused",
                SocketError.TimedOut => "Timed out",
                SocketError.NetworkUnreachable => "Network unreachable",
                _ => "Socket error: " + ex.SocketErrorCode
            };
        }
        catch (Exception ex)
        {
            return "Failed: " + ex.Message;
        }
    }
}
