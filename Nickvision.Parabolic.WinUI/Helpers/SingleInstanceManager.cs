using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.WinUI.Helpers;

/// <summary>
/// Coordinates the unpackaged WinUI application so protocol activations are
/// forwarded to the already running instance instead of being discarded.
/// </summary>
public static class SingleInstanceManager
{
    private const string MutexName = @"Local\Nickvision.Parabolic.WinUI";
    private const string PipeName = "Nickvision.Parabolic.WinUI.SingleInstance";
    private static Mutex? _mutex;
    private static CancellationTokenSource? _cancellationTokenSource;
    private static Task? _listenerTask;

    /// <summary>
    /// Attempts to become the primary application instance.
    /// </summary>
    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
        }
        return createdNew;
    }

    /// <summary>
    /// Forwards command-line arguments to the primary instance.
    /// </summary>
    public static bool ForwardArguments(string[] args)
    {
        // The primary process may still be starting after it acquires the mutex.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(250);
                using var writer = new BinaryWriter(client);
                writer.Write(args.Length);
                foreach (var arg in args)
                {
                    writer.Write(arg);
                }
                writer.Flush();
                return true;
            }
            catch (TimeoutException)
            {
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
        return false;
    }

    /// <summary>
    /// Starts listening for protocol activations from later instances.
    /// </summary>
    public static void StartListening(Action<string[]> handler)
    {
        if (_listenerTask is not null)
        {
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;
        _listenerTask = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(cancellationToken);
                    using var reader = new BinaryReader(server);
                    var count = reader.ReadInt32();
                    if (count < 0 || count > 100)
                    {
                        continue;
                    }
                    var args = new string[count];
                    for (var i = 0; i < count; i++)
                    {
                        args[i] = reader.ReadString();
                    }
                    handler(args);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (EndOfStreamException)
                {
                    // A secondary instance disconnected before sending a full message.
                }
                catch (IOException)
                {
                    // Recreate the pipe and keep accepting future activations.
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Releases all single-instance resources.
    /// </summary>
    public static void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _listenerTask = null;
        if (_mutex is not null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
