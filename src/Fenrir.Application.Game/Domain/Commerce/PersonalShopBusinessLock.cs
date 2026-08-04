namespace Fenrir.Application.Game.Domain.Commerce;

public static class PersonalShopBusinessLock
{
    private const int GateCount = 1024;

    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, GateCount)
        .Select(static _ => new SemaphoreSlim(1, 1))
        .ToArray();

    public static async ValueTask<Lease> AcquireAsync(int shopOwnerCharacterId, CancellationToken cancellationToken)
    {
        var gate = Gates[(int)((uint)shopOwnerCharacterId % GateCount)];
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(gate);
    }

    public sealed class Lease : IDisposable
    {
        private SemaphoreSlim? _gate;

        internal Lease(SemaphoreSlim gate)
        {
            _gate = gate;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _gate, null)?.Release();
        }
    }
}
