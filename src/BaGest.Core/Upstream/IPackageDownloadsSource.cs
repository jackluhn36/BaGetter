using System.Collections.Generic;
using System.Threading.Tasks;

namespace BaGest.Core;

public interface IPackageDownloadsSource
{
    public Task<Dictionary<string, Dictionary<string, long>>> GetPackageDownloadsAsync();
}
