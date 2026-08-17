using Microsoft.Extensions.Logging;
using Nickvision.Desktop.Application;

namespace Nickvision.Parabolic.Shared.Services;

/// <summary>
/// Keeps browser-owned downloads separate from the desktop application's
/// interactive recovery queue. Both processes can therefore use overlapping
/// in-memory download identifiers without corrupting each other's state.
/// </summary>
public sealed class BackgroundRecoveryService : RecoveryService
{
    public BackgroundRecoveryService(
        ILogger<RecoveryService> logger,
        IConfigurationService configurationService,
        IDatabaseService databaseService)
        : base(logger, configurationService, databaseService, "browser_recovery_queue")
    {
    }
}
