using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Social;

namespace Fenrir.Data.Social;

public sealed record MentorRepository(ICaeriusNetDbContext Db) : IMentorRepository
{

        public async ValueTask<CharacterMentorDto?> GetForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterMentor_GetForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<CharacterMentorDto>(sp, ct);
    }

        public async ValueTask BondAsync(int masterCharacterId, int studentCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterMentor_Bond", 0)
            .AddParameter("MasterCharacterId", masterCharacterId, SqlDbType.Int)
            .AddParameter("StudentCharacterId", studentCharacterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask ClearForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_CharacterMentor_ClearForCharacter", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
