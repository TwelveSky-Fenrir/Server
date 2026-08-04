using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.World;

public interface IZone195NokSanStateRepository
{
    public ValueTask<(Zone195NokSanStateRowDto? State, ImmutableArray<Zone195NokSanCaptureRowDto> Captures)>
        LoadAsync(CancellationToken ct);

        public ValueTask<bool> TrySaveAsync(Zone195NokSanStateRowDto state,
        ImmutableArray<Zone195NokSanCaptureRowDto> captures, CancellationToken ct);
}
