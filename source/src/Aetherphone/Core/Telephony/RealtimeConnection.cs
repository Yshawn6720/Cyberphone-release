using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Aetherphone.Core.Net;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal sealed class RealtimeConnection : IDisposable
{
    private const int ReceiveBufferBytes = 16 * 1024;
    private const int MaxMessageBytes = 1024 * 1024;
    private static readonly TimeSpan HealthyConnectionThreshold = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);
    private readonly RealtimeEndpoint endpoint;
    private readonly Action<ClientWebSocketOptions> configureOptions;
    private readonly object gate = new();
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private CancellationTokenSource? lifetime;
    private ClientWebSocket? socket;
    private volatile bool connected;

    public RealtimeConnection(RealtimeEndpoint endpoint)
    {
        this.endpoint = endpoint;
        configureOptions = ConfigureOptions;
    }

    public bool Connected => connected;
    public event Action<CallControl>? ControlReceived;
    public event Action<byte[]>? MediaReceived;
    public event Action<bool>? ConnectedChanged;
    public event Action<RelayFailure>? Failed;

    public void Start()
    {
        lock (gate)
        {
            if (lifetime is not null)
            {
                return;
            }

            lifetime = new CancellationTokenSource();
            var token = lifetime.Token;
            _ = Task.Run(() => RunAsync(token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? toCancel;
        ClientWebSocket? toAbort;
        lock (gate)
        {
            toCancel = lifetime;
            lifetime = null;
            toAbort = socket;
            socket = null;
        }

        toCancel?.Cancel();
        try
        {
            toAbort?.Abort();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Realtime abort failed");
        }

        toAbort?.Dispose();
        toCancel?.Dispose();
        SetConnected(false);
    }

    private async Task RunAsync(CancellationToken token)
    {
        var attempt = 0;
        while (!token.IsCancellationRequested)
        {
            var connectedAtUtc = DateTime.MinValue;
            int? refusedStatus = null;
            RelayFailure? refusal = null;
            WebSocketCloseStatus? closeStatus = null;
            string? closeDescription = null;
            try
            {
                var realtimeUri = endpoint.ResolveUri();
                using var ws = await OfficialHostGuard.ConnectWebSocketAsync(realtimeUri, configureOptions, token)
                    .ConfigureAwait(false);
                lock (gate)
                {
                    socket = ws;
                }

                connectedAtUtc = DateTime.UtcNow;
                SetConnected(true);
                refusal = await ReceiveLoopAsync(ws, token).ConfigureAwait(false);
                closeStatus = ws.CloseStatus;
                closeDescription = ws.CloseStatusDescription;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                refusedStatus = (exception as GuardedWebSocketException)?.StatusCode;
                AepLog.Warning(exception, "Realtime connection error");
            }
            finally
            {
                lock (gate)
                {
                    socket = null;
                }

                SetConnected(false);
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            if (connectedAtUtc != DateTime.MinValue)
            {
                AepLog.Info($"[realtime] closed code={(closeStatus is { } status ? (int)status : 0)} "
                    + $"description={closeDescription ?? string.Empty} heldSeconds={(DateTime.UtcNow - connectedAtUtc).TotalSeconds:F0}");
            }

            if (refusal is { } refused)
            {
                Abandon(token, refused);
                return;
            }

            var heldLongEnough = connectedAtUtc != DateTime.MinValue
                && DateTime.UtcNow - connectedAtUtc >= HealthyConnectionThreshold;
            attempt = heldLongEnough ? 1 : attempt + 1;
            if (endpoint.MaxReconnects() is { } maximum && (attempt > maximum || IsFinalRefusal(refusedStatus)))
            {
                Abandon(token, RelayFailure.FromStatus(refusedStatus));
                return;
            }

            var seconds = attempt == 1
                ? 0.5d + Random.Shared.NextDouble() * 4.5d
                : Math.Min(15d, Math.Pow(2, Math.Min(attempt - 1, 4))) * (0.5d + Random.Shared.NextDouble());
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ConfigureOptions(ClientWebSocketOptions options)
    {
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.KeepAliveTimeout = TimeSpan.FromSeconds(20);
        endpoint.Configure(options);
    }

    private static bool IsFinalRefusal(int? status) => status is (int)HttpStatusCode.BadRequest
        or (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden or (int)HttpStatusCode.NotFound
        or (int)HttpStatusCode.Conflict or (int)HttpStatusCode.TooManyRequests
        or (int)HttpStatusCode.InternalServerError or (int)HttpStatusCode.ServiceUnavailable;

    private void Abandon(CancellationToken token, RelayFailure failure)
    {
        CancellationTokenSource? owned;
        lock (gate)
        {
            owned = lifetime;
            if (owned is null || owned.Token != token)
            {
                return;
            }

            lifetime = null;
        }

        owned.Dispose();
        AepLog.Info($"[realtime] stopped reconnecting status={failure.Status} refusal={failure.Refusal}");
        if (failure.Refusal != RelayRefusal.None)
        {
            endpoint.Refused?.Invoke(failure);
        }

        Failed?.Invoke(failure);
    }

    private async Task<RelayFailure?> ReceiveLoopAsync(ClientWebSocket ws, CancellationToken token)
    {
        RelayFailure? refusal = null;
        var buffer = new byte[ReceiveBufferBytes];
        using var message = new MemoryStream();
        try
        {
            while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                message.SetLength(0);
                ValueWebSocketReceiveResult chunk;
                do
                {
                    chunk = await ws.ReceiveAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                    if (chunk.MessageType == WebSocketMessageType.Close)
                    {
                        return refusal;
                    }

                    message.Write(buffer, 0, chunk.Count);
                    if (message.Length > MaxMessageBytes)
                    {
                        AepLog.Warning("Realtime message exceeded the size limit; dropping the connection.");
                        return refusal;
                    }
                } while (!chunk.EndOfMessage);

                if (chunk.MessageType == WebSocketMessageType.Text)
                {
                    refusal = DispatchControl(message.GetBuffer(), (int)message.Length) ?? refusal;
                }
                else
                {
                    MediaReceived?.Invoke(message.ToArray());
                }
            }
        }
        catch (Exception exception) when (refusal is not null && exception is WebSocketException or IOException)
        {
            AepLog.Warning(exception, "Realtime socket dropped after the relay refused it");
        }

        return refusal;
    }

    private RelayFailure? DispatchControl(byte[] data, int length)
    {
        try
        {
            var control = JsonSerializer.Deserialize(new ReadOnlySpan<byte>(data, 0, length),
                TelephonyJsonContext.Default.CallControl);
            if (control is null)
            {
                return null;
            }

            if (string.Equals(control.Type, SignalType.RelayRefused, StringComparison.Ordinal))
            {
                return RelayFailure.FromRefusal(control);
            }

            ControlReceived?.Invoke(control);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Realtime control parse failed");
        }

        return null;
    }

    public Task SendControlAsync(CallControl control)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(control, TelephonyJsonContext.Default.CallControl);
        return SendAsync(payload, WebSocketMessageType.Text);
    }

    public Task SendMediaAsync(byte[] frame)
    {
        return SendAsync(frame, WebSocketMessageType.Binary);
    }

    private async Task SendAsync(byte[] payload, WebSocketMessageType type)
    {
        ClientWebSocket? ws;
        lock (gate)
        {
            ws = socket;
        }

        if (ws is null || ws.State != WebSocketState.Open)
        {
            if (type == WebSocketMessageType.Text)
            {
                AepLog.Warning($"[realtime] send-dropped socket={(ws is null ? "none" : ws.State.ToString())}");
            }

            return;
        }

        await sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            using var timeout = new CancellationTokenSource(SendTimeout);
            await ws.SendAsync(payload, type, true, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            AepLog.Warning("Realtime send timed out; dropping the connection.");
            ws.Abort();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Realtime send failed");
        }
        finally
        {
            sendLock.Release();
        }
    }

    private void SetConnected(bool value)
    {
        if (connected == value)
        {
            return;
        }

        connected = value;
        ConnectedChanged?.Invoke(value);
    }

    public void Dispose()
    {
        Stop();
        sendLock.Dispose();
    }
}
