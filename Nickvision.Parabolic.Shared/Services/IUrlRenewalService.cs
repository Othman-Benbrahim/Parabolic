using Nickvision.Parabolic.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.Shared.Services;

public interface IUrlRenewalService
{
    Task RenewAsync(DownloadOptions options, CancellationToken cancellationToken);
}
