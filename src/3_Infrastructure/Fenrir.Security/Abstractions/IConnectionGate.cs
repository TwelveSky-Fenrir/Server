using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Fenrir.Security.Abstractions;

public interface IConnectionGate
{

        ValueTask<ConnectionVerdict> EvaluateAsync(IPAddress remoteAddress, CancellationToken cancellationToken);
}
