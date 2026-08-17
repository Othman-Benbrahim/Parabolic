using System;
using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.NativeHost;

public sealed class NativeMessagingTransport : IAsyncDisposable
{
    private const int MaxMessageBytes = 16 * 1024 * 1024;

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly SemaphoreSlim _writeLock;

    public NativeMessagingTransport(Stream input, Stream output)
    {
        _input = input;
        _output = output;
        _writeLock = new SemaphoreSlim(1, 1);
    }

    internal async Task<NativeRequest?> ReadRequestAsync(CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        if (!await ReadExactlyOrEofAsync(_input, header, cancellationToken))
        {
            return null;
        }
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaxMessageBytes)
        {
            throw new InvalidDataException($"Invalid Native Messaging payload length: {length}.");
        }
        var payload = new byte[length];
        await _input.ReadExactlyAsync(payload, cancellationToken);
        return JsonSerializer.Deserialize(payload, NativeJsonContext.Default.NativeRequest)
            ?? throw new JsonException("Native Messaging request was empty.");
    }

    internal Task WriteResponseAsync(NativeResponse response, CancellationToken cancellationToken) =>
        WriteAsync(response, NativeJsonContext.Default.NativeResponse, cancellationToken);

    internal Task WriteEventAsync(NativeEventEnvelope message, CancellationToken cancellationToken) =>
        WriteAsync(message, NativeJsonContext.Default.NativeEventEnvelope, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        await _input.DisposeAsync();
        await _output.DisposeAsync();
    }

    private async Task WriteAsync<T>(T message, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, jsonTypeInfo);
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _output.WriteAsync(header, cancellationToken);
            await _output.WriteAsync(payload, cancellationToken);
            await _output.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static async Task<bool> ReadExactlyOrEofAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken);
            if (count == 0)
            {
                if (read == 0)
                {
                    return false;
                }
                throw new EndOfStreamException("Native Messaging frame ended inside its length header.");
            }
            read += count;
        }
        return true;
    }
}
