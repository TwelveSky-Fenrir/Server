using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Social;

/// <summary>
///     game.Characters.TeacherCharacterId/StudentCharacterId access (Phase C/V6 Social --
///     CZ_TEACHER_* family, contracts/05_social.md). "Mentor" here means the legacy's "teacher/student"
///     relationship -- named to avoid colliding with the already-established <c>Mentor*</c> wire/opcode
///     naming (Opcodes.Zone.Incoming/Outgoing.Mentor*) this batch's handlers implement against.
/// </summary>
public sealed record MentorRepository(ICaeriusNetDbContext Db) : IMentorRepository
{
    /// <summary>Loaded once at world entry (AVATAR_INFO's Teacher/Student fields).</summary>
    public async ValueTask<CharacterMentorDto?> GetForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterMentor_GetForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<CharacterMentorDto>(sp, ct);
    }

    /// <summary>CZ_TEACHER_START_SEND (opcode 62) -- bonds both sides atomically in one transaction.</summary>
    public async ValueTask BondAsync(int masterCharacterId, int studentCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterMentor_Bond", 0)
            .AddParameter("MasterCharacterId", masterCharacterId, SqlDbType.Int)
            .AddParameter("StudentCharacterId", studentCharacterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>CZ_TEACHER_END_SEND (opcode 63) -- clears BOTH pointers on the calling character's own row only; the partner side is deliberately left untouched (preserved legacy asymmetry, see the proc's own header).</summary>
    public async ValueTask ClearForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterMentor_ClearForCharacter", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
