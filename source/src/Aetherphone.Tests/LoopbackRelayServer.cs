using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Aetherphone.Tests;

internal sealed class LoopbackRelayServer : IDisposable
{
    private const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const ushort KickedCloseCode = 4003;
    // RFC 6455 section 5.2: opcode 1 is a text frame, a client masks every payload with a 4 byte key, and a
    // payload length of 126 or 127 announces a 2 or 8 byte extended length.
    private const int TextOpcode = 1;
    private const int MaskBytes = 4;
    private const int ShortLengthMarker = 126;
    private const int LongLengthMarker = 127;
    private static readonly TimeSpan GreetingHold = TimeSpan.FromMilliseconds(300);

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource shutdown = new();
    private readonly ConcurrentQueue<string> requests = new();
    private readonly ConcurrentQueue<string> texts = new();
    private readonly ConcurrentQueue<TcpClient> clients = new();
    private readonly int refusalStatus;
    private readonly string? farewell;
    private readonly ushort farewellCloseCode;
    private readonly bool abortAfterFarewell;
    private readonly Func<string, byte[]>? respond;
    private readonly string? greeting;
    private readonly string[]? answers;
    private readonly Task accepting;
    private int connectionCount;
    private int closedCount;
    private long receivedBytes;
    private int greeted;

    public LoopbackRelayServer(int refusalStatus = 0, string? farewell = null, Func<string, byte[]>? respond = null,
        string? greeting = null, ushort farewellCloseCode = KickedCloseCode, bool abortAfterFarewell = false,
        string[]? answers = null)
    {
        this.refusalStatus = refusalStatus;
        this.farewell = farewell;
        this.farewellCloseCode = farewellCloseCode;
        this.abortAfterFarewell = abortAfterFarewell;
        this.respond = respond;
        this.greeting = greeting;
        this.answers = answers;
        listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        accepting = AcceptAsync();
    }

    public string Url { get; }

    public int ConnectionCount => Volatile.Read(ref connectionCount);

    public int ClosedCount => Volatile.Read(ref closedCount);

    public long ReceivedBytes => Interlocked.Read(ref receivedBytes);

    public string[] Requests => requests.ToArray();

    public string[] ReceivedTexts => texts.ToArray();

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

            if (respond is not null)
            {
                await stream.WriteAsync(respond(request), shutdown.Token).ConfigureAwait(false);
                return;
            }

            var key = HeaderValue(request, "Sec-WebSocket-Key") ?? string.Empty;
            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + HandshakeGuid)));
            var upgrade = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
                + $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(upgrade), shutdown.Token).ConfigureAwait(false);
            if (farewell is not null && abortAfterFarewell)
            {
                await stream.WriteAsync(TextFrame(farewell), shutdown.Token).ConfigureAwait(false);
                await Task.Delay(GreetingHold, shutdown.Token).ConfigureAwait(false);
                client.Client.LingerState = new LingerOption(true, 0);
                return;
            }

            if (farewell is not null)
            {
                await stream.WriteAsync(FarewellFrames(farewell, farewellCloseCode), shutdown.Token).ConfigureAwait(false);
            }

            var buffer = new byte[4096];
            var incoming = new List<byte>();
            if (greeting is not null && Interlocked.Exchange(ref greeted, 1) == 0)
            {
                Collect(incoming, buffer, await stream.ReadAsync(buffer, shutdown.Token).ConfigureAwait(false));
                await stream.WriteAsync(TextFrame(greeting), shutdown.Token).ConfigureAwait(false);
                await Task.Delay(GreetingHold, shutdown.Token).ConfigureAwait(false);
                return;
            }

            var pendingAnswers = answers;
            int read;
            while ((read = await stream.ReadAsync(buffer, shutdown.Token).ConfigureAwait(false)) > 0)
            {
                Collect(incoming, buffer, read);
                if (pendingAnswers is null)
                {
                    continue;
                }

                foreach (var answer in pendingAnswers)
                {
                    await stream.WriteAsync(TextFrame(answer), shutdown.Token).ConfigureAwait(false);
                }

                pendingAnswers = null;
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

    private void Collect(List<byte> incoming, byte[] buffer, int count)
    {
        Interlocked.Add(ref receivedBytes, count);
        for (var index = 0; index < count; index++)
        {
            incoming.Add(buffer[index]);
        }

        while (TryTakeFrame(incoming, out var opcode, out var payload))
        {
            if (opcode == TextOpcode)
            {
                texts.Enqueue(Encoding.UTF8.GetString(payload));
            }
        }
    }

    private static bool TryTakeFrame(List<byte> incoming, out int opcode, out byte[] payload)
    {
        opcode = 0;
        payload = [];
        if (incoming.Count < 2)
        {
            return false;
        }

        var length = incoming[1] & 0x7F;
        var offset = 2;
        if (length == ShortLengthMarker)
        {
            if (incoming.Count < 4)
            {
                return false;
            }

            length = (incoming[2] << 8) | incoming[3];
            offset = 4;
        }
        else if (length == LongLengthMarker)
        {
            if (incoming.Count < 10)
            {
                return false;
            }

            length = 0;
            for (var index = 2; index < 10; index++)
            {
                length = (length << 8) | incoming[index];
            }

            offset = 10;
        }

        var masked = (incoming[1] & 0x80) != 0;
        var maskOffset = offset;
        if (masked)
        {
            offset += MaskBytes;
        }

        if (incoming.Count < offset + length)
        {
            return false;
        }

        payload = new byte[length];
        for (var index = 0; index < length; index++)
        {
            var value = incoming[offset + index];
            payload[index] = masked ? (byte)(value ^ incoming[maskOffset + index % MaskBytes]) : value;
        }

        opcode = incoming[0] & 0x0F;
        incoming.RemoveRange(0, offset + length);
        return true;
    }

    private static byte[] FarewellFrames(string text, ushort closeCode)
    {
        var frames = new List<byte>(TextFrame(text));
        frames.AddRange([0x88, 2, (byte)(closeCode >> 8), (byte)(closeCode & 0xFF)]);
        return frames.ToArray();
    }

    private static byte[] TextFrame(string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var frame = new List<byte> { 0x81 };
        if (payload.Length < 126)
        {
            frame.Add((byte)payload.Length);
        }
        else
        {
            frame.Add(126);
            frame.Add((byte)(payload.Length >> 8));
            frame.Add((byte)payload.Length);
        }

        frame.AddRange(payload);
        return frame.ToArray();
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
