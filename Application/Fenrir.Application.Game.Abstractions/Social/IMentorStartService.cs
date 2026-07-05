using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public interface IMentorStartService
{
    /// <summary>CZ_TEACHER_START_SEND -- consumes the accepted negotiation; only the master may call this.</summary>
    public bool TryConsumeStart(int masterId, out int studentId);

    /// <summary>Bonds both sides in one transaction and mirrors the student's side via a zone command.</summary>
    public ValueTask BondAsync(PlayerRuntimeState master, PlayerRuntimeState student, Zone studentZone,
        CancellationToken cancellationToken);
}
