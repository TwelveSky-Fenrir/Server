using Fenrir.Application.Game.World;
using Fenrir.Data.Social;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Discriminator for how a CZ_TEACHER_END_SEND attempt resolved.</summary>
public enum MentorEndResultKind
{
    NotBonded,
    Ended
}

public readonly record struct MentorEndResult(MentorEndResultKind Kind);

public interface IMentorEndService
{
    ValueTask<MentorEndResult> EndAsync(PlayerRuntimeState state, CancellationToken cancellationToken);
}

/// <summary>
///     Clears only the caller's own pointers; the partner's opposite pointer is deliberately left untouched
///     (legacy asymmetry).
/// </summary>
public sealed class MentorEndService(IMentorRepository repository) : IMentorEndService
{
    public async ValueTask<MentorEndResult> EndAsync(PlayerRuntimeState state, CancellationToken cancellationToken)
    {
        if (state.TeacherCharacterId is null && state.StudentCharacterId is null)
            return new MentorEndResult(MentorEndResultKind.NotBonded);

        await repository.ClearForCharacterAsync(state.CharacterId, cancellationToken);

        state.TeacherCharacterId = null;
        state.StudentCharacterId = null;

        return new MentorEndResult(MentorEndResultKind.Ended);
    }
}
