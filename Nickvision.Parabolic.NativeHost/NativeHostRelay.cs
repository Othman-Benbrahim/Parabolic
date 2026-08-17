using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.NativeHost;

internal static class NativeHostRelay
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var pipe = await ConnectToServiceAsync(cancellationToken);
        await using var input = Console.OpenStandardInput();
        await using var output = Console.OpenStandardOutput();
        using var relayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var browserToService = input.CopyToAsync(pipe, relayCancellation.Token);
        var serviceToBrowser = pipe.CopyToAsync(output, relayCancellation.Token);
        await Task.WhenAny(browserToService, serviceToBrowser);
        relayCancellation.Cancel();
        try
        {
            await Task.WhenAll(browserToService, serviceToBrowser);
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static async Task<NamedPipeClientStream> ConnectToServiceAsync(CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var pipe = new NamedPipeClientStream(
                ".",
                DaemonProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(500, cancellationToken);
                return pipe;
            }
            catch (Exception exception) when (exception is IOException or TimeoutException)
            {
                lastException = exception;
                await pipe.DisposeAsync();
                if (attempt == 0)
                {
                    StartService();
                }
                await Task.Delay(250, cancellationToken);
            }
        }
        throw new IOException("The persistent Parabolic download service did not start in time.", lastException);
    }

    private static void StartService()
    {
        var executablePath = Path.Combine(AppContext.BaseDirectory, DaemonProtocol.ServiceExecutableName);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "The Parabolic download service is missing. Reinstall Parabolic with the Windows installer.",
                executablePath);
        }
        Process.Start(new ProcessStartInfo(executablePath, "--background")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        });
    }
}
