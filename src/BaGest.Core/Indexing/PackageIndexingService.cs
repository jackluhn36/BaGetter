using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace BaGest.Core;

public class PackageIndexingService : IPackageIndexingService
{
    private const string DescriptionElement = "description";

    private readonly IPackageDatabase _packages;
    private readonly IPackageStorageService _storage;
    private readonly ISearchIndexer _search;
    private readonly SystemTime _time;
    private readonly IOptionsSnapshot<BaGestOptions> _options;
    private readonly IOptionsSnapshot<RetentionOptions> _retentionOptions;
    private readonly ILogger<PackageIndexingService> _logger;
    private readonly IPackageDeletionService _packageDeletionService;

    public PackageIndexingService(
        IPackageDatabase packages,
        IPackageStorageService storage,
        IPackageDeletionService packageDeletionService,
        ISearchIndexer search,
        SystemTime time,
        IOptionsSnapshot<BaGestOptions> options,
        IOptionsSnapshot<RetentionOptions> retentionOptions,
        ILogger<PackageIndexingService> logger)
    {
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _retentionOptions = retentionOptions ?? throw new ArgumentNullException(nameof(retentionOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _packageDeletionService = packageDeletionService ?? throw new ArgumentNullException(nameof(packageDeletionService));
#pragma warning disable CS0618 // Type or member is obsolete
        if (_options.Value.MaxVersionsPerPackage > 0)
        {
            _logger.LogError("MaxVersionsPerPackage is deprecated and is not used. Please use MaxMajorVersions, MaxMinorVersions, MaxPatchVersions, and MaxPrereleaseVersions instead.");
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public Task<PackageIndexingResult> IndexAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        return IndexAsync(stream, null, cancellationToken);
    }

    public async Task<PackageIndexingResult> IndexAsync(Stream packageStream, string? cacheFeedUrl = null, CancellationToken cancellationToken = default)
    {
        // Try to extract all the necessary information from the package.
        Package? package;
        Stream? nuspecStream;
        Stream? readmeStream;
        Stream? iconStream;

        try
        {
            using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);

            // Packages cached from an upstream feed were already accepted there, so only pushes are validated.
            if (cacheFeedUrl == null)
            {
                var validationResult = ValidateManifest(packageReader.NuspecReader);
                if (validationResult != PackageIndexingResult.Success)
                {
                    return validationResult;
                }
            }

            package = packageReader.GetPackageMetadata();
            package.CachedFrom = cacheFeedUrl;
            package.Published = _time.UtcNow;

            nuspecStream = await packageReader.GetNuspecAsync(cancellationToken);
            nuspecStream = await nuspecStream.AsTemporaryFileStreamAsync(cancellationToken);

            if (package.HasReadme)
            {
                readmeStream = await packageReader.GetReadmeAsync(cancellationToken);
                readmeStream = await readmeStream.AsTemporaryFileStreamAsync(cancellationToken);
            }
            else
            {
                readmeStream = null;
            }

            if (package.HasEmbeddedIcon)
            {
                iconStream = await packageReader.GetIconAsync(cancellationToken);
                iconStream = await iconStream.AsTemporaryFileStreamAsync(cancellationToken);
            }
            else
            {
                iconStream = null;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Uploaded package is invalid");

            return PackageIndexingResult.InvalidPackage;
        }

        // The package is well-formed. Ensure this is a new package.
        if (await _packages.ExistsAsync(package.Id, package.Version, cancellationToken))
        {
            if (_options.Value.AllowPackageOverwrites == PackageOverwriteAllowed.False ||
                (_options.Value.AllowPackageOverwrites == PackageOverwriteAllowed.PrereleaseOnly && !package.IsPrerelease))
            {
                return PackageIndexingResult.PackageAlreadyExists;
            }

            await _packages.HardDeletePackageAsync(package.Id, package.Version, cancellationToken);
            await _storage.DeleteAsync(package.Id, package.Version, cancellationToken);
        }

        // TODO: Add more package validations
        // TODO: Call PackageArchiveReader.ValidatePackageEntriesAsync
        _logger.LogInformation(
            "Validated package {PackageId} {PackageVersion}, persisting content to storage...",
            package.Id,
            package.NormalizedVersionString);

        try
        {
            packageStream.Position = 0;

            await _storage.SavePackageContentAsync(
                package,
                packageStream,
                nuspecStream,
                readmeStream,
                iconStream,
                cancellationToken);
        }
        catch (PackageStorageConflictException e)
        {
            // The database has no entry for this package (checked above), yet the storage already holds a
            // different file at the same path. Overwriting is deliberately avoided: the file may belong to a
            // package that a concurrent push is just about to register.
            _logger.LogWarning(
                e,
                "Package {PackageId} {PackageVersion} is not in the database, but the storage already contains a different file at {Path}. " +
                "The storage and the database are likely out of sync. Remove the orphaned files from the storage and push again.",
                package.Id,
                package.NormalizedVersionString,
                e.Path);

            return PackageIndexingResult.StorageConflict;
        }
        catch (Exception e)
        {
            // This may happen due to concurrent pushes.
            // TODO: Make IPackageStorageService.SavePackageContentAsync return a result enum so this
            // can be properly handled.
            _logger.LogError(
                e,
                "Failed to persist package {PackageId} {PackageVersion} content to storage",
                package.Id,
                package.NormalizedVersionString);

            throw;
        }

        _logger.LogInformation(
            "Persisted package {Id} {Version} content to storage, saving metadata to database...",
            package.Id,
            package.NormalizedVersionString);

        var result = await _packages.AddAsync(package, cancellationToken);
        if (result == PackageAddResult.PackageAlreadyExists)
        {
            _logger.LogWarning(
                "Package {Id} {Version} metadata already exists in database",
                package.Id,
                package.NormalizedVersionString);

            return PackageIndexingResult.PackageAlreadyExists;
        }

        if (result != PackageAddResult.Success)
        {
            _logger.LogError($"Unknown {nameof(PackageAddResult)} value: {{PackageAddResult}}", result);

            throw new InvalidOperationException($"Unknown {nameof(PackageAddResult)} value: {result}");
        }

        _logger.LogInformation(
            "Successfully persisted package {Id} {Version} metadata to database. Indexing in search...",
            package.Id,
            package.NormalizedVersionString);

        await _search.IndexAsync(package, cancellationToken);

        if (_retentionOptions.Value.HasAnyLimit)
        {
            try
            {
                _logger.LogInformation(
                    "Deleting older packages for package {PackageId} {PackageVersion}",
                    package.Id,
                    package.NormalizedVersionString);

                var deleted = await _packageDeletionService.DeleteOldVersionsAsync(
                    package,
                    _retentionOptions.Value.MaxMajorVersions,
                    _retentionOptions.Value.MaxMinorVersions,
                    _retentionOptions.Value.MaxPatchVersions,
                    _retentionOptions.Value.MaxPrereleaseVersions,
                    _retentionOptions.Value.DeletePrereleasesOfOlderMajors,
                    cancellationToken);
                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Deleted {packages} older packages for package {PackageId} {PackageVersion}",
                        deleted,
                        package.Id,
                        package.NormalizedVersionString);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    "Failed to cleanup older versions of package {PackageId} {PackageVersion}",
                    package.Id,
                    package.NormalizedVersionString);
            }
        }

        _logger.LogInformation(
            "Successfully indexed package {Id} {Version} in search",
            package.Id,
            package.NormalizedVersionString);

        return PackageIndexingResult.Success;
    }

    // Mirrors what nuget.org rejects on push: a manifest without a description, and dependency
    // version ranges that do not parse strictly. nuget.org accepts a manifest without authors,
    // so authors are not required here either.
    private PackageIndexingResult ValidateManifest(NuspecReader nuspec)
    {
        if (!HasDescription(nuspec))
        {
            _logger.LogWarning(
                "Pushed package {PackageId} {PackageVersion} has no description",
                nuspec.GetId(),
                nuspec.GetVersion());

            return PackageIndexingResult.MissingDescription;
        }

        try
        {
            _ = nuspec.GetDependencyGroups(useStrictVersionCheck: true).ToList();
        }
        catch (PackagingException e)
        {
            _logger.LogWarning(
                e,
                "Pushed package {PackageId} {PackageVersion} has a dependency with an invalid version range",
                nuspec.GetId(),
                nuspec.GetVersion());

            return PackageIndexingResult.InvalidDependencyVersionRange;
        }

        return PackageIndexingResult.Success;
    }

    // nuget.org treats the description as missing when the manifest metadata has no description
    // entry, which is also the case for an empty description element.
    private static bool HasDescription(NuspecReader nuspec)
    {
        return nuspec
            .GetMetadata()
            .Any(entry => string.Equals(entry.Key, DescriptionElement, StringComparison.OrdinalIgnoreCase));
    }
}
