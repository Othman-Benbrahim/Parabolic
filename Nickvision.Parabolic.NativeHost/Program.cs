using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nickvision.Desktop.Application;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.NativeHost;

internal static class Program
{
    private static async Task<int> Main()
    {
        using var shutdown = new CancellationTokenSource();
        try
        {
            var builder = Host.CreateApplicationBuilder([]);
            builder.ConfigureParabolic([]);
            builder.Logging.ClearProviders();
            using var host = builder.Build();
            await host.StartAsync(shutdown.Token);

            await using var transport = new NativeMessagingTransport(
                Console.OpenStandardInput(),
                Console.OpenStandardOutput());
            using var server = new NativeMessagingServer(
                transport,
                host.Services.GetRequiredService<IConfigurationService>(),
                host.Services.GetRequiredService<IDiscoveryService>(),
                host.Services.GetRequiredService<IDownloadService>(),
                host.Services.GetRequiredService<IYtdlpExecutableService>());
            await server.RunAsync(shutdown.Token);

            shutdown.Cancel();
            await host.Services.GetRequiredService<IDownloadService>().StopAllAsync();
            await host.StopAsync();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Parabolic Native Messaging host failed: {exception}");
            return 1;
        }
    }
}
