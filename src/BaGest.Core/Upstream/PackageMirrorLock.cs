using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace BaGest.Core;

/// <summary>
/// Serializes mirroring of a single package version within one BaGest instance, so concurrent
/// requests for the same not yet cached package download, store and index it only once.
/// Requests for different packages never wait for each other. Register it as a singleton.
/// </summary>
public sealed class PackageMirrorLock
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Waits until no other request is mirroring the given package version.
    /// </summary>
    /// <param name="id">The package ID.</param>
    /// <param name="version">The package version.</param>
    /// <param name="cancellationToken">A token to stop waiting.</param>
    /// <returns>A handle that releases the lock when disposed.</returns>
    public async Task<IDisposable> AcquireAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(version);

        var key = $"{id}/{version.ToNormalizedString()}".ToLowerInvariant();

        Entry? entry;
        lock (_entries)
        {
            if (!_entries.TryGetValue(key, out entry))
            {
                entry = new Entry();
                _entries.Add(key, entry);
            }

            entry.References++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
        }
        catch
        {
            RemoveReference(key, entry);
            throw;
        }

        return new Releaser(this, key, entry);
    }

    private void RemoveReference(string key, Entry entry)
    {
        lock (_entries)
        {
            entry.References--;
            if (entry.References == 0)
            {
                _entries.Remove(key);
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int References { get; set; }
    }

    private sealed class Releaser(PackageMirrorLock owner, string key, Entry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            entry.Semaphore.Release();
            owner.RemoveReference(key, entry);
        }
    }
}
