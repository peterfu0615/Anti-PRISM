using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Serilog;

namespace AntiPrism.Core.Ipc;

public class NamedPipeServer : IAsyncDisposable
{
    public const string PipeName = "AntiPrism";
    private readonly Func<IpcRequest, Task<IpcResponse>> _requestHandler;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenTask;
    private readonly List<NamedPipeServerStream> _connectedStreams = new();
    private readonly object _streamsLock = new();

    public NamedPipeServer(Func<IpcRequest, Task<IpcResponse>> requestHandler)
    {
        _requestHandler = requestHandler;
    }

    public void Start()
    {
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        Log.Information("NamedPipeServer started on pipe: \\\\.\\pipe\\{PipeName}", PipeName);
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? serverStream = null;
            try
            {
                var pipeSecurity = CreatePipeSecurity();
                serverStream = NamedPipeServerStreamAcl.Create(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 65536,
                    outBufferSize: 65536,
                    pipeSecurity);

                await serverStream.WaitForConnectionAsync(ct);

                lock (_streamsLock)
                {
                    _connectedStreams.Add(serverStream);
                }

                _ = Task.Run(() => HandleClientAsync(serverStream, ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error accepting pipe connection");
                serverStream?.Dispose();
                await Task.Delay(500, ct);
            }
        }
    }

    private static PipeSecurity CreatePipeSecurity()
    {
        var ps = new PipeSecurity();
        // Allow LocalSystem full control
        ps.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // Allow Builtin Administrators full control
        ps.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // Allow Authenticated Users ReadWrite to talk to service
        ps.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return ps;
    }

    private async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            while (!ct.IsCancellationRequested && stream.IsConnected)
            {
                // Read 4-byte message length
                int length = reader.ReadInt32();
                if (length <= 0 || length > 10 * 1024 * 1024) break;

                var bytes = reader.ReadBytes(length);
                var json = Encoding.UTF8.GetString(bytes);

                var request = JsonSerializer.Deserialize<IpcRequest>(json);
                if (request == null) continue;

                var response = await _requestHandler(request);
                var respJson = JsonSerializer.Serialize(response);
                var respBytes = Encoding.UTF8.GetBytes(respJson);

                writer.Write(respBytes.Length);
                writer.Write(respBytes);
                writer.Flush();
            }
        }
        catch (EndOfStreamException)
        {
            // Client disconnected normally
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Client pipe connection terminated");
        }
        finally
        {
            lock (_streamsLock)
            {
                _connectedStreams.Remove(stream);
            }
            stream.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        lock (_streamsLock)
        {
            foreach (var stream in _connectedStreams)
            {
                try { stream.Dispose(); } catch { }
            }
            _connectedStreams.Clear();
        }

        if (_listenTask != null)
        {
            try { await _listenTask; } catch { }
        }
        _cts.Dispose();
    }
}
