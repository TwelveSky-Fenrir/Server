using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     Clears only the caller's own pointers; the partner's opposite pointer is deliberately left untouched
///     (legacy asymmetry).
/// </summary>
public sealed class MentorEndService(IMentorRepository repository, ILogger<MentorEndService> logger)
    : IMentorEndService
{
    public async ValueTask<MentorEndResult> EndAsync(PlayerRuntimeState state, CancellationToken cancellationToken)
    {
        if (state.TeacherCharacterId is null && state.StudentCharacterId is null)
        {
            // Client-visible as a session disconnect (MentorEndHandler aborts on this outcome).
            logger.LogWarning(
                "Mentor end rejected: character {CharacterId} is not bonded as either teacher or student -- session will be disconnected",
                state.CharacterId);
            return new MentorEndResult(MentorEndResultKind.NotBonded);
        }

        await repository.ClearForCharacterAsync(state.CharacterId, cancellationToken);

        var wasTeacher = state.TeacherCharacterId;
        var wasStudent = state.StudentCharacterId;
        state.TeacherCharacterId = null;
        state.StudentCharacterId = null;

        logger.LogInformation(
            "Mentor bond ended: character {CharacterId} cleared its own pointers (was teacher {WasTeacher}, was student {WasStudent})",
            state.CharacterId, wasTeacher, wasStudent);

        return new MentorEndResult(MentorEndResultKind.Ended);
    }
}
