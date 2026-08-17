using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nickvision.Parabolic.NativeHost;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Services;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.DownloadService;

internal static class Program
{
    private const string MutexName = "Local\\Nickvision.Parabolic.DownloadService";

    private static async Task<int> Main(string[] args)
    {
        using var singleInstance = new Mutex(false, MutexName);
        if (!singleInstance.WaitOne(0))
        {
            return 0;
        }

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        try
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.ConfigureParabolic(args);
            builder.Logging.ClearProviders();
            builder.Services.RemoveAll<IRecoveryService>();
            builder.Services.AddSingleton<IRecoveryService, BackgroundRecoveryService>();
            builder.Services.AddSingleton<PersistentDownloadCoordinator>();
            using var host = builder.Build();
            await host.StartAsync(shutdown.Token);

            var coordinator = host.Services.GetRequiredService<PersistentDownloadCoordinator>();
            var recoveryService = host.Services.GetRequiredService<IRecoveryService>();
            if (recoveryService.Count > 0)
            {
                await coordinator.RecoverAllAsync();
            }

            await RunPipeServerAsync(host.Services, coordinator, shutdown.Token);
            await host.StopAsync();
            return 0;
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Parabolic persistent download service failed: {exception}");
            return 1;
        }
        finally
        {
            singleInstance.ReleaseMutex();
        }
    }

    private static async Task RunPipeServerAsync(
        IServiceProvider services,
        PersistentDownloadCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var clients = new List<Task>();
        while (!cancellationToken.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                DaemonProtocol.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
            }
            catch
            {
                await pipe.DisposeAsync();
                throw;
            }
            clients.RemoveAll(task => task.IsCompleted);
            clients.Add(HandleClientAsync(pipe, services, coordinator, cancellationToken));
        }
        await Task.WhenAll(clients);
    }

    private static async Task HandleClientAsync(
        NamedPipeServerStream pipe,
        IServiceProvider services,
        PersistentDownloadCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        await using var transport = new NativeMessagingTransport(pipe, pipe);
        using var server = new NativeMessagingServer(
            transport,
            services.GetRequiredService<Nickvision.Desktop.Application.IConfigurationService>(),
            services.GetRequiredService<IDiscoveryService>(),
            services.GetRequiredService<IDownloadService>(),
            services.GetRequiredService<IYtdlpExecutableService>(),
            coordinator);
        await server.RunAsync(cancellationToken);
    }
}
