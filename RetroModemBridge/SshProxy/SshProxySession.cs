using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace RetroModemBridge.SshProxy
{
    /// <summary>
    /// Bridges a vintage computer serial session to an interactive SSH shell.
    /// RetroModem Bridge handles SSH. The retro computer only sees plain terminal text.
    /// </summary>
    public sealed class SshProxySession : IDisposable
    {
        private SshClient? _client;
        private ShellStream? _shell;
        private CancellationTokenSource? _cts;
        private readonly object _writeLock = new object();

        public bool IsConnected => _client?.IsConnected == true && _shell != null;

        public event Action<byte[]>? DataReceived;
        public event Action<string>? Log;
        public event Action? Disconnected;

        public async Task ConnectAsync(
            string host,
            int port,
            string username,
            string password,
            string terminalName = "ansi",
            uint columns = 80,
            uint rows = 24,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("SSH host is required.", nameof(host));

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("SSH username is required.", nameof(username));

            if (port <= 0)
                port = 22;

            Disconnect();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await Task.Run(() =>
            {
                Log?.Invoke($"SSH connecting to {host}:{port} as {username}...");

                var auth = new PasswordAuthenticationMethod(username, password ?? string.Empty);
                var connectionInfo = new ConnectionInfo(host, port, username, auth)
                {
                    Timeout = TimeSpan.FromSeconds(20)
                };

                _client = new SshClient(connectionInfo)
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(30)
                };

                _client.ErrorOccurred += (_, e) => Log?.Invoke("SSH error: " + e.Exception.Message);
                _client.HostKeyReceived += (_, e) =>
                {
                    // First version accepts host keys automatically.
                    // Future version should store and compare fingerprints.
                    Log?.Invoke("SSH host key: " + BitConverter.ToString(e.FingerPrint).Replace("-", ":"));
                    e.CanTrust = true;
                };

                _client.Connect();

                // Use an interactive shell, not RunCommand, because this is a live terminal bridge.
                _shell = _client.CreateShellStream(
                    terminalName,
                    columns,
                    rows,
                    columns * 8,
                    rows * 16,
                    8192);

                _shell.DataReceived += Shell_DataReceived;

                Log?.Invoke("SSH connected.");
            }, _cts.Token).ConfigureAwait(false);
        }

        public void SendFromSerial(byte[] buffer, int offset, int count)
        {
            if (!IsConnected || _shell == null || buffer == null || count <= 0)
                return;

            try
            {
                lock (_writeLock)
                {
                    _shell.Write(buffer, offset, count);
                    _shell.Flush();
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke("SSH write failed: " + ex.Message);
                Disconnect();
            }
        }

        public void SendFromSerial(string text)
        {
            if (text == null) return;
            var bytes = Encoding.ASCII.GetBytes(text);
            SendFromSerial(bytes, 0, bytes.Length);
        }

        private void Shell_DataReceived(object? sender, ShellDataEventArgs e)
        {
            try
            {
                if (e.Data != null && e.Data.Length > 0)
                    DataReceived?.Invoke(e.Data);
            }
            catch (Exception ex)
            {
                Log?.Invoke("SSH read failed: " + ex.Message);
                Disconnect();
            }
        }

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
            }
            catch { }

            try
            {
                if (_shell != null)
                {
                    _shell.DataReceived -= Shell_DataReceived;
                    _shell.Dispose();
                }
            }
            catch { }

            try
            {
                if (_client != null)
                {
                    if (_client.IsConnected)
                        _client.Disconnect();

                    _client.Dispose();
                }
            }
            catch { }

            _shell = null;
            _client = null;
            _cts?.Dispose();
            _cts = null;

            Disconnected?.Invoke();
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
