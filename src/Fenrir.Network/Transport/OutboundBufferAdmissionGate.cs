namespace Fenrir.Network.Transport;

public sealed class OutboundBufferAdmissionGate
{
    private readonly object _gate = new();
    private int _currentPendingFrames;
    private long _currentPendingBytes;

    public OutboundBufferAdmissionGate(int maxPendingFrames, long maxPendingBytes)
    {
        if (maxPendingFrames <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPendingFrames));
        if (maxPendingBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPendingBytes));

        MaxPendingFrames = maxPendingFrames;
        MaxPendingBytes = maxPendingBytes;
    }

    public int MaxPendingFrames { get; }

    public long MaxPendingBytes { get; }

    public int CurrentPendingFrames => Volatile.Read(ref _currentPendingFrames);

    public long CurrentPendingBytes => Volatile.Read(ref _currentPendingBytes);

    public bool TryReserve(int byteCount)
    {
        if (byteCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(byteCount));

        lock (_gate)
        {
            if (_currentPendingFrames >= MaxPendingFrames ||
                _currentPendingBytes > MaxPendingBytes - byteCount)
            {
                return false;
            }

            _currentPendingFrames++;
            _currentPendingBytes += byteCount;
            return true;
        }
    }

    public void Release(int byteCount, int frameCount)
    {
        if (byteCount < 0)
            throw new ArgumentOutOfRangeException(nameof(byteCount));
        if (frameCount < 0)
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        if (byteCount == 0 && frameCount == 0)
            return;

        lock (_gate)
        {
            if (frameCount > _currentPendingFrames || byteCount > _currentPendingBytes)
                throw new InvalidOperationException("Outbound buffer admission was released more than once.");

            _currentPendingFrames -= frameCount;
            _currentPendingBytes -= byteCount;
        }
    }
}
