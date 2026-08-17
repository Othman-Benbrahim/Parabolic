using Nickvision.Parabolic.Shared.Models;
using System.Diagnostics;

namespace Nickvision.Parabolic.Shared.Services;

public interface INm3u8dlExecutableService
{
    Process GetDownloadProcess(DownloadOptions downloadOptions);
}
