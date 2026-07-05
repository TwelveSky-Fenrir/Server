using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Social;

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
