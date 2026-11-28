# Import NuGet.Server packages

[NuGet.Server](https://github.com/NuGet/NuGet.Server) is a lightweight standalone NuGet server. It is strongly recommended that you upgrade to BaGest if you use NuGet.Server. Feel free to open a [GitHub issue](https://github.com/BeerSolutions/BaGest/issues) if you need help migrating.

:::info

Please refer to the [BaGest vs NuGet.Server](../vs/nugetserver.md) page for reasons to upgrade to BaGest.

:::

## Steps

Make sure that you've installed [nuget.exe](https://www.nuget.org/downloads). In PowerShell, run:

```powershell
$source = "<NuGet.Server package source>"
$destination = "<BaGest package source>"
```

If you've [configured BaGest to require an API Key](https://beersolutions.github.io/BaGest/docs/configuration#requiring-an-api-key), set it using [the `setapikey` command](https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-setapikey):

```powershell
& nuget.exe setapikey "MY-API-KEY" -Source $destination
```

Now run the following PowerShell script:

```powershell
if (!(Test-Path "Web.config")) {
  throw "Please run this script in the same directory as NuGet.Server's Web.config file"
}

(& nuget.exe list -AllVersions -Source $source).Split([Environment]::NewLine) | % {
  $id = $_.Split(" ")[0].Trim()
  $version = $_.Split(" ")[1].Trim()

  $path = [IO.Path]::Combine("Packages", $id, $version, "${id}.${version}.nupkg")

  Write-Host "nuget.exe push -Source $destination ""$path"""
  & nuget.exe push -Source $destination $path
}
```
