namespace Fenrir.Application.Game.Domain.World.Loot;

public sealed class ProcessWideGroundItemSerialGenerator : IGroundItemSerialGenerator
{
    private static int _lastAllocatedSerial;

    public int Generate(in GroundItemSerialGenerationRequest request)
    {
        _ = request;

        while (true)
        {
            var current = Volatile.Read(ref _lastAllocatedSerial);
            if (current == int.MaxValue)
                throw new InvalidOperationException("The process-wide ground-item serial range is exhausted.");

            var next = current + 1;
            if (Interlocked.CompareExchange(ref _lastAllocatedSerial, next, current) == current)
                return next;
        }
    }
}
