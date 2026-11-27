using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BaGest.Core;

/// <summary>
/// The result of attempting to index a package.
/// See <see cref="IPackageIndexingService.IndexAsync(Stream, string, CancellationToken)"/>.
/// </summary>
public enum PackageIndexingResult
{
    /// <summary>
    /// The package is malformed. This may also happen if BaGest is in a corrupted state.
    /// </summary>
    InvalidPackage,

    /// <summary>
    /// The package has already been indexed.
    /// </summary>
    PackageAlreadyExists,

    /// <summary>
    /// The package's content could not be stored because a different file already exists in the storage
    /// at the same path, while the database has no entry for the package. This usually means the storage
    /// and the database are out of sync, or that the same package is being pushed concurrently.
    /// </summary>
    StorageConflict,

    /// <summary>
    /// The package has been indexed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// A dependency of the pushed package has a version range that cannot be parsed.
    /// </summary>
    InvalidDependencyVersionRange,

    /// <summary>
    /// The pushed package's manifest has no description.
    /// </summary>
    MissingDescription,
}

/// <summary>
/// The service used to accept new packages.
/// </summary>
public interface IPackageIndexingService
{
    /// <summary>
    /// Attempt to index a new package.
    /// </summary>
    /// <param name="stream">The stream containing the package's content.</param>
    /// <param name="cacheFeedUrl">The url of the feed the package has been cached from in case of pull through caching, else null.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The result of the attempted indexing operation.</returns>
    public Task<PackageIndexingResult> IndexAsync(Stream stream, string cacheFeedUrl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempt to index a new package.
    /// </summary>
    /// <param name="stream">The stream containing the package's content.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The result of the attempted indexing operation.</returns>
    public Task<PackageIndexingResult> IndexAsync(Stream stream, CancellationToken cancellationToken = default);
}
