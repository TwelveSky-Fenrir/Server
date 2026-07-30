using System.Net;

namespace Fenrir.Security.Abstractions;

public interface IConnectionGate
{
    public ValueTask<ConnectionVerdict> EvaluateAsync(IPAddress remoteAddress, CancellationToken cancellationToken);
}
