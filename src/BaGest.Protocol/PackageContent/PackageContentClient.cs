using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BaGest.Protocol.Models;
using NuGet.Versioning;

namespace BaGest.Protocol;

public partial class NuGetClientFactory
{
    private class PackageContentClient : IPackageContentClient
    {
        private readonly NuGetClientFactory _clientfactory;

        public PackageContentClient(NuGetClientFactory clientFactory)
        {
            _clientfactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        }

        public async Task<Stream?> DownloadPackageOrNullAsync(
            string packageId,
            NuGetVersion packageVersion,
            CancellationToken cancellationToken = default)
        {
            var client = await _clientfactory.GetPackageContentClientAsync(cancellationToken);

            return client is null ? null : await client.DownloadPackageOrNullAsync(packageId, packageVersion, cancellationToken);
        }

        public async Task<Stream?> DownloadPackageManifestOrNullAsync(
            string packageId,
            NuGetVersion packageVersion,
            CancellationToken cancellationToken = default)
        {
            var client = await _clientfactory.GetPackageContentClientAsync(cancellationToken);

            return client is null ? null : await client.DownloadPackageManifestOrNullAsync(packageId, packageVersion, cancellationToken);
        }

        public async Task<PackageVersionsResponse?> GetPackageVersionsOrNullAsync(
            string packageId,
            CancellationToken cancellationToken = default)
        {
            var client = await _clientfactory.GetPackageContentClientAsync(cancellationToken);

            return client is null ? null : await client.GetPackageVersionsOrNullAsync(packageId, cancellationToken);
        }
    }
}
