using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Discriminator for how a CZ_TEACHER_END_SEND attempt resolved.</summary>
public enum MentorEndResultKind
{
    NotBonded,
    Ended
}

public readonly record struct MentorEndResult(MentorEndResultKind Kind);

public interface IMentorEndService
{
    public ValueTask<MentorEndResult> EndAsync(PlayerRuntimeState state, CancellationToken cancellationToken);
}
