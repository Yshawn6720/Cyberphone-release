using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Aetherphone.Tests;

internal sealed class LoopbackRelayServer : IDisposable
{
    private const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource shutdown = new();
    private readonly ConcurrentQueue<string> requests = new();
    private readonly ConcurrentQueue<TcpClient> clients = new();
    private readonly int refusalStatus;
    private readonly Task accepting;
    private int connectionCount;
    private int closedCount;
    private long receivedBytes;

    public LoopbackRelayServer(int refusalStatus = 0)
    {
        this.refusalStatus = refusalStatus;
        listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        accepting = AcceptAsync();
    }

    public string Url { get; }

    public int ConnectionCount => Volatile.Read(ref connectionCount);

    public int ClosedCount => Volatile.Read(ref closedCount);

    public long ReceivedBytes => Interlocked.Read(ref receivedBytes);

    public string[] Requests => requests.ToArray();

    public static string RequestLine(string request) =>
        request[..request.IndexOf("\r\n", StringComparison.Ordinal)];

    public static string? HeaderValue(string request, string name)
    {
        var lines = request.Split("\r\n");
        for (var index = 1; index < lines.Length; index++)
        {
            var separator = lines[index].IndexOf(':', StringComparison.Ordinal);
            if (separator > 0 && lines[index][..separator].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return lines[index][(separator + 1)..].Trim();
            }
        }

        return null;
    }

    public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }

        return true;
    }

    public void Dispose()
    {
        shutdown.Cancel();
        listener.Stop();
        while (clients.TryDequeue(out var client))
        {
            client.Dispose();
        }

        accepting.Wait(TimeSpan.FromSeconds(5));
        shutdown.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!shutdown.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(shutdown.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException
                                                  or ObjectDisposedException)
            {
                return;
            }

            clients.Enqueue(client);
            Interlocked.Increment(ref connectionCount);
            _ = ServeAsync(client);
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var request = await ReadRequestAsync(stream).ConfigureAwait(false);
            if (request is null)
            {
                return;
            }

            requests.Enqueue(request);
            if (refusalStatus != 0)
            {
                var refusal = $"HTTP/1.1 {refusalStatus} Refused\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(refusal), shutdown.Token).ConfigureAwait(false);
                return;
            }

            var key = HeaderValue(request, "Sec-WebSocket-Key") ?? string.Empty;
            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + HandshakeGuid)));
            var upgrade = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
                + $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(upgrade), shutdown.Token).ConfigureAwait(false);
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, shutdown.Token).ConfigureAwait(false)) > 0)
            {
                Interlocked.Add(ref receivedBytes, read);
            }
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException
                                              or ObjectDisposedException or SocketException)
        {
        }
        finally
        {
            Interlocked.Increment(ref closedCount);
            client.Dispose();
        }
    }

    private async Task<string?> ReadRequestAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        var received = new StringBuilder();
        while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer, shutdown.Token).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            received.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        return received.ToString();
    }
}
