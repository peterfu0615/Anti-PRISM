using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Serilog;

namespace AntiPrism.Core.Ipc;

public class NamedPipeClient : IDisposable
{
    private readonly string _pipeName;
    private NamedPipeClientStream? _clientStream;
    private BinaryReader? _reader;
    private BinaryWriter? _writer;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public NamedPipeClient(string pipeName = NamedPipeServer.PipeName)
    {
        _pipeName = pipeName;
    }

    public bool IsConnected => _clientStream?.IsConnected == true;

    public async Task<bool> ConnectAsync(int timeoutMs = 3000, CancellationToken ct = default)
    {
        try
        {
            Disconnect();
            _clientStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await _clientStream.ConnectAsync(timeoutMs, ct);

            _reader = new BinaryReader(_clientStream, Encoding.UTF8, leaveOpen: true);
            _writer = new BinaryWriter(_clientStream, Encoding.UTF8, leaveOpen: true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to connect to Named Pipe {PipeName}", _pipeName);
            Disconnect();
            return false;
        }
    }

    public async Task<IpcResponse> SendCommandAsync(IpcCommandType command, string? payloadJson = null, int timeoutMs = 5000)
    {
        var request = new IpcRequest
        {
            Command = command,
            PayloadJson = payloadJson
        };

        await _sendLock.WaitAsync();
        try
        {
            if (!IsConnected)
            {
                bool connected = await ConnectAsync(timeoutMs);
                if (!connected || _writer == null || _reader == null)
                {
                    return IpcResponse.Fail(request.RequestId, "AntiPrism Service is not reachable.");
                }
            }

            var json = JsonSerializer.Serialize(request);
            var bytes = Encoding.UTF8.GetBytes(json);

            _writer!.Write(bytes.Length);
            _writer.Write(bytes);
            _writer.Flush();

            int respLength = _reader!.ReadInt32();
            if (respLength <= 0 || respLength > 10 * 1024 * 1024)
            {
                return IpcResponse.Fail(request.RequestId, "Invalid response size from service.");
            }

            var respBytes = _reader.ReadBytes(respLength);
            var respJson = Encoding.UTF8.GetString(respBytes);

            return JsonSerializer.Deserialize<IpcResponse>(respJson) ??
                   IpcResponse.Fail(request.RequestId, "Failed to deserialize response.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error communicating with service over pipe");
            Disconnect();
            return IpcResponse.Fail(request.RequestId, $"IPC Communication error: {ex.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Disconnect()
    {
        try
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _clientStream?.Dispose();
        }
        catch { }
        finally
        {
            _reader = null;
            _writer = null;
            _clientStream = null;
        }
    }

    public void Dispose()
    {
        Disconnect();
        _sendLock.Dispose();
    }
}
