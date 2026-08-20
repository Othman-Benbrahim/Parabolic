using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Nickvision.Parabolic.NativeHost;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Services;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
#if WINDOWS
using System.Security.AccessControl;
using System.Security.Principal;
#endif
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.DownloadService;

internal static class Program
{
    private static string MutexName => OperatingSystem.IsWindows()
        ? "Local\\Nickvision.Parabolic.DownloadService"
        : "Nickvision.Parabolic.DownloadService";

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
            var pipe = CreatePipeServer();
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

    private static NamedPipeServerStream CreatePipeServer()
    {
#if WINDOWS
        // PipeOptions.CurrentUserOnly also requires the client and server to
        // have the same Windows elevation level. Firefox normally launches
        // Native Messaging hosts without elevation, while an installer may
        // have started this per-user service elevated. Use an explicit ACL so
        // both processes can communicate while still restricting the pipe to
        // the Windows account that owns the service.
        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
        var user = identity.User
            ?? throw new InvalidOperationException("Unable to identify the Windows user for the Parabolic pipe.");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(user);
        security.AddAccessRule(new PipeAccessRule(
            user,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(
            DaemonProtocol.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
#else
        // On Linux and macOS, .NET implements named pipes with Unix domain
        // sockets. CurrentUserOnly rejects cross-user clients and keeps the
        // Firefox relay scoped to the account that owns the service.
        return new NamedPipeServerStream(
            DaemonProtocol.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
#endif
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
            coordinator,
            services.GetRequiredService<IHttpClientFactory>());
        await server.RunAsync(cancellationToken);
    }
}
