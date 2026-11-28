using System.Threading;
using System.Threading.Tasks;

namespace BaGest.Core;

public interface IAuthenticationService
{
    public Task<bool> AuthenticateAsync(string? apiKey, CancellationToken cancellationToken);
}
