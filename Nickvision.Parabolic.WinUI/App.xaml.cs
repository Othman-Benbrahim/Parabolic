using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Nickvision.Parabolic.Shared.Controllers;
using Nickvision.Parabolic.Shared.Services;
using Nickvision.Parabolic.WinUI.Helpers;
using Nickvision.Parabolic.WinUI.Views;
using System;
using System.Runtime.InteropServices;

namespace Nickvision.Parabolic.WinUI;

public partial class App : Application
{
    [LibraryImport("user32.dll")]
    private static partial int SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial int ShowWindow(IntPtr hWnd, int nCmdShow);

    private readonly IServiceProvider _serviceProvider;
    private readonly IEventsService _eventsService;
    private Window? _window;

    public App(IServiceProvider serviceProvider, IEventsService eventsService)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _eventsService = eventsService;
        UnhandledException += (_, e) =>
        {
            _serviceProvider.GetRequiredService<ILogger<App>>().LogError(e.Exception, $"An unhandled exception occurred: {e.Message}");
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_window is null)
        {
            _window = _serviceProvider.GetRequiredService<MainWindow>();
        }
        _window.Activate();
        SingleInstanceManager.StartListening(ProcessForwardedArguments);
    }

    private void ProcessForwardedArguments(string[] args)
    {
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            _window.Activate();
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            ShowWindow(windowHandle, 9); // SW_RESTORE
            SetForegroundWindow(windowHandle);
            var url = MainWindowController.ParseUrlFromArguments(args);
            if (url is not null)
            {
                _eventsService.InvokeDownloadRequested(url);
            }
        });
    }
}
