using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace BaGest.Web.Tests;

/// <summary>
/// Keeps libman.json and wwwroot/lib describing the same files. The CI build also restores
/// wwwroot/lib from scratch and fails on any difference, which covers versions and contents.
/// </summary>
public class ClientLibrariesFacts
{
    private const string LibFolderPrefix = "wwwroot/lib/";

    private static readonly string WebProjectDirectory = FindWebProjectDirectory();
    private static readonly string LibDirectory = Path.Combine(WebProjectDirectory, "wwwroot", "lib");

    [Fact]
    public void LibraryFolderContainsExactlyTheFilesDeclaredInLibman()
    {
        var declared = DeclaredFiles().ToHashSet(StringComparer.Ordinal);
        var present = Directory.EnumerateFiles(LibDirectory, "*", SearchOption.AllDirectories)
            .Select(f => Normalize(Path.GetRelativePath(WebProjectDirectory, f)))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(declared.Except(present).Order());
        Assert.Empty(present.Except(declared).Order());
    }

    [Fact]
    public void EveryLibraryReferencedByAPageIsDeclaredInLibman()
    {
        var declared = DeclaredFiles().ToHashSet(StringComparer.Ordinal);
        var referenced = Directory.EnumerateFiles(Path.Combine(WebProjectDirectory, "Pages"), "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(page => Regex.Matches(File.ReadAllText(page), @"~/_content/BaGest\.Web/(?<path>lib/[^""?]+)"))
            .Select(m => "wwwroot/" + m.Groups["path"].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(referenced);
        Assert.All(referenced, path => Assert.Contains(path, declared));
    }

    [Fact]
    public void EveryLibraryFolderShipsALicense()
    {
        var folders = DeclaredFiles()
            .GroupBy(f => f[..f.IndexOf('/', LibFolderPrefix.Length)])
            .ToList();

        Assert.NotEmpty(folders);
        Assert.All(folders, folder => Assert.Contains(folder, f => Path.GetFileName(f).StartsWith("LICENSE", StringComparison.Ordinal)));
    }

    private static IEnumerable<string> DeclaredFiles()
    {
        using var libman = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebProjectDirectory, "libman.json")));

        foreach (var library in libman.RootElement.GetProperty("libraries").EnumerateArray())
        {
            var destination = Normalize(library.GetProperty("destination").GetString()!).TrimEnd('/');
            Assert.StartsWith(LibFolderPrefix, destination + "/", StringComparison.Ordinal);
            Assert.NotEqual(LibFolderPrefix, destination + "/");

            foreach (var file in library.GetProperty("files").EnumerateArray())
            {
                yield return Normalize($"{destination}/{file.GetString()}");
            }
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string FindWebProjectDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "BaGest.Web");
            if (File.Exists(Path.Combine(candidate, "libman.json")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not find src/BaGest.Web/libman.json above the test output directory.");
    }
}
