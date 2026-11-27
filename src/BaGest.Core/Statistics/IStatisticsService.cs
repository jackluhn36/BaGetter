using System.Collections.Generic;
using System.Threading.Tasks;

namespace BaGest.Core.Statistics;

public interface IStatisticsService
{
    public Task<int> GetPackagesTotalAmount();
    public Task<int> GetVersionsTotalAmount();
    public IEnumerable<string> GetKnownServices();
}
