using System;
using System.Threading;
using System.Threading.Tasks;
using BaGest.Protocol.Models;

namespace BaGest.Protocol;

public partial class NuGetClientFactory
{
    private class CatalogClient : ICatalogClient
    {
        private readonly NuGetClientFactory _clientfactory;

        public CatalogClient(NuGetClientFactory clientFactory)
        {
            _clientfactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        }

        public async Task<CatalogIndex?> GetIndexAsync(CancellationToken cancellationToken = default)
        {
            var client = await _clientfactory.GetCatalogClientAsync(cancellationToken);

            return client is null
                ? null
                : await client.GetIndexAsync(cancellationToken);
        }

        public async Task<CatalogPage?> GetPageAsync(string pageUrl, CancellationToken cancellationToken = default)
        {
            var client = await _clientfactory.GetCatalogClientAsync(cancellationToken);

            return client is null
                ? null
                : await client.GetPageAsync(pageUrl, cancellationToken) ?? null;
        }

        public async Task<PackageDetailsCatalogLeaf?> GetPackageDetailsLeafAsync(string leafUrl, CancellationToken cancellationToken = default)
        {
            var client = await _clientfactory.GetCatalogClientAsync(cancellationToken);

            return client is null
                ? null
                : await client.GetPackageDetailsLeafAsync(leafUrl, cancellationToken);
        }

        public async Task<PackageDeleteCatalogLeaf?> GetPackageDeleteLeafAsync(string leafUrl, CancellationToken cancellationToken = default)
        {
            var client = await _clientfactory.GetCatalogClientAsync(cancellationToken);

            return client is null
                ? null
                : await client.GetPackageDeleteLeafAsync(leafUrl, cancellationToken);
        }
    }
}
