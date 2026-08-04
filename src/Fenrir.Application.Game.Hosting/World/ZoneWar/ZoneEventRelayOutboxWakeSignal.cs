using System.Threading.Channels;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public interface IZoneEventRelayOutboxWakeSignal
{
    public void Signal();

    public ValueTask WaitAsync(CancellationToken ct);
}

public sealed class ZoneEventRelayOutboxWakeSignal : IZoneEventRelayOutboxWakeSignal
{
    private readonly Channel<byte> _signals = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.DropWrite
    });

    public void Signal()
    {
        _signals.Writer.TryWrite(0);
    }

    public async ValueTask WaitAsync(CancellationToken ct)
    {
        await _signals.Reader.ReadAsync(ct).ConfigureAwait(false);
    }
}
