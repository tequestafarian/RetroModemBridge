using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace RetroModemBridge;

public sealed class SshProxySession : IDisposable
{
    private SshClient? _client;
    private ShellStream? _shell;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public bool IsConnected => _client?.IsConnected == true && _shell is not null;

    public event Action<byte[]>? BytesReceived;
    public event Action<string>? Log;
    public event Action? Disconnected;

    public async Task ConnectAsync(SshDialProfile profile, string password, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            throw new InvalidOperationException("SSH session is already connected.");

        if (profile is null)
            throw new ArgumentNullException(nameof(profile));

        Log?.Invoke($"SSH connecting to {profile.DisplayName}...");

        var connectionInfo = new PasswordConnectionInfo(profile.Host, profile.Port, profile.Username, password)
        {
            Timeout = TimeSpan.FromSeconds(20),
            Encoding = Encoding.UTF8
        };

        connectionInfo.HostKeyReceived += (_, e) =>
        {
            var fingerprint = BitConverter.ToString(e.FingerPrint).Replace("-", ":");
            Log?.Invoke($"SSH host key fingerprint: {fingerprint}");
            e.CanTrust = true; // first pass: auto-trust. Known-host storage can be added later.
        };

        _client = new SshClient(connectionInfo);

        await Task.Run(() => _client.Connect(), cancellationToken).ConfigureAwait(false);

        _shell = _client.CreateShellStream(
            terminalName: "ansi",
            columns: 80,
            rows: 24,
            width: 800,
            height: 600,
            bufferSize: 4096);

        _readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token), CancellationToken.None);

        Log?.Invoke($"SSH connected to {profile.DisplayName}.");
    }

    public Task SendBytesAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (bytes is null || bytes.Length == 0)
            return Task.CompletedTask;

        if (!IsConnected || _shell is null)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _shell.Write(bytes, 0, bytes.Length);
            _shell.Flush();
        }, cancellationToken);
    }

    public Task SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            return Task.CompletedTask;

        return SendBytesAsync(Encoding.ASCII.GetBytes(text), cancellationToken);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected && _shell is not null)
            {
                var bytesRead = await _shell.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (bytesRead <= 0)
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var output = new byte[bytesRead];
                Array.Copy(buffer, output, bytesRead);
                BytesReceived?.Invoke(output);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal disconnect path.
        }
        catch (SshConnectionException ex)
        {
            Log?.Invoke("SSH connection error: " + ex.Message);
        }
        catch (ObjectDisposedException)
        {
            // Normal disconnect path.
        }
        catch (Exception ex)
        {
            Log?.Invoke("SSH read error: " + ex.Message);
        }
        finally
        {
            Disconnected?.Invoke();
        }
    }

    public void Disconnect()
    {
        try
        {
            _readCts?.Cancel();
        }
        catch { }

        try
        {
            _shell?.Dispose();
            _shell = null;
        }
        catch { }

        try
        {
            if (_client?.IsConnected == true)
                _client.Disconnect();
            _client?.Dispose();
            _client = null;
        }
        catch { }

        Log?.Invoke("SSH disconnected.");
    }

    public void Dispose()
    {
        Disconnect();
        _readCts?.Dispose();
    }
}
