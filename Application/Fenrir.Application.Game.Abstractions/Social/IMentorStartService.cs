using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public interface IMentorStartService
{
    /// <summary>CZ_TEACHER_START_SEND -- consumes the accepted negotiation; only the master may call this.</summary>
    /// <remarks>
    ///     LEGACY-PARITY RISK (open item, not resolved by inference): whether this master-only restriction is
    ///     legacy-accurate is unconfirmed -- see
    ///     <see cref="Fenrir.Application.Game.Domain.Social.Mentor.MentorRegistry.TryConsumeStart" /> for the
    ///     full citation (Server/ts25zone/S04_MyWork02.cpp:9406-9499) and correction against the original,
    ///     uncorroborated "roles fixed at ask-time" finding this restriction was built on.
    /// </remarks>
    public bool TryConsumeStart(int masterId, out int studentId);

    /// <summary>Bonds both sides in one transaction and mirrors the student's side via a zone command.</summary>
    public ValueTask BondAsync(PlayerRuntimeState master, PlayerRuntimeState student, Zone studentZone,
        CancellationToken cancellationToken);
}
