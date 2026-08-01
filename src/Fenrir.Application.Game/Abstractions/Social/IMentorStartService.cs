using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public interface IMentorStartService
{
    public bool TryConsumeStart(int masterId, out int studentId);

    public ValueTask BondAsync(PlayerRuntimeState master, PlayerRuntimeState student, Zone studentZone,
        CancellationToken cancellationToken);
}
