using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorStartService(
    MentorRegistry mentors,
    IMentorRepository repository,
    ILogger<MentorStartService> logger)
    : IMentorStartService
{
    public bool TryConsumeStart(int masterId, out int studentId)
    {
        if (!mentors.TryConsumeStart(masterId, out studentId))
        {
            logger.LogDebug("Mentor start ignored: character {MasterId} has no accepted negotiation to consume",
                masterId);
            return false;
        }

        return true;
    }

    public async ValueTask BondAsync(PlayerRuntimeState master, PlayerRuntimeState student, Zone studentZone,
        CancellationToken cancellationToken)
    {
        await repository.BondAsync(master.CharacterId, student.CharacterId, cancellationToken);

        master.StudentCharacterId = student.CharacterId;

        if (!studentZone.PostMentorCommand(new MentorZoneCommand(student.CharacterId, master.CharacterId)))
            logger.LogError(
                "Zone {MapId} mentor inbox full: dropped TeacherCharacterId mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                studentZone.MapId, student.CharacterId);

        logger.LogInformation(
            "Mentor bond established: master {MasterId} ({MasterName}) <-> student {StudentId} ({StudentName})",
            master.CharacterId, master.Name, student.CharacterId, student.Name);
    }
}
