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
            await NativeHostRelay.RunAsync(shutdown.Token);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Parabolic Native Messaging host failed: {exception}");
            return 1;
        }
    }
}
