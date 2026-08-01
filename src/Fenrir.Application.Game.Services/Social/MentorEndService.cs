using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorEndService(IMentorRepository repository, ILogger<MentorEndService> logger)
    : IMentorEndService
{
    public async ValueTask<MentorEndResult> EndAsync(PlayerRuntimeState state, CancellationToken cancellationToken)
    {
        if (state.TeacherCharacterId is null && state.StudentCharacterId is null)
        {
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
