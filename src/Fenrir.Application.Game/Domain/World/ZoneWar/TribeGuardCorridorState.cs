namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class TribeGuardCorridorState
{
    public const int TribeCount = 4;
    public const int SegmentCount = 4;
    private readonly Lock _lock = new();

    private readonly bool[,] _open = new bool[TribeCount, SegmentCount];

    public bool IsOpen(byte tribeId, byte segmentIndex)
    {
        ValidateTribeId(tribeId);
        ValidateSegmentIndex(segmentIndex);

        lock (_lock)
        {
            return _open[tribeId, segmentIndex];
        }
    }

    public bool TrySetOpen(byte tribeId, byte segmentIndex, bool isOpen)
    {
        ValidateTribeId(tribeId);
        ValidateSegmentIndex(segmentIndex);

        lock (_lock)
        {
            if (_open[tribeId, segmentIndex] == isOpen)
                return false;

            _open[tribeId, segmentIndex] = isOpen;
            return true;
        }
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (tribeId >= TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }

    private static void ValidateSegmentIndex(byte segmentIndex)
    {
        if (segmentIndex >= SegmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex), segmentIndex,
                $"SegmentIndex must be 0-{SegmentCount - 1}.");
    }
}
