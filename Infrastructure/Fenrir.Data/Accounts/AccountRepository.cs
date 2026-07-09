using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Accounts;

namespace Fenrir.Data.Accounts;

// Facade over auth.usp_Account_*. AccountId is the legacy uUserIdx; usp_Account_Create returns the new IDENTITY value, callers never mint ids.
public sealed record AccountRepository(ICaeriusNetDbContext Db) : IAccountRepository
{
    public async ValueTask<AuthenticateAccountDto?> AuthenticateAsync(string loginName, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("auth", "usp_Account_Authenticate", 1)
            .AddParameter("LoginName", loginName, SqlDbType.NVarChar)
            .Build();

        return await Db.FirstQueryAsync<AuthenticateAccountDto>(sp, ct);
    }

    public async ValueTask<int> CreateAsync(string loginName, byte[] passwordHash, byte[] passwordSalt,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("auth", "usp_Account_Create", 1)
            .AddParameter("LoginName", loginName, SqlDbType.NVarChar)
            .AddParameter("PasswordHash", passwordHash, SqlDbType.VarBinary)
            .AddParameter("PasswordSalt", passwordSalt, SqlDbType.VarBinary)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    public async ValueTask RecordLoginAttemptAsync(int accountId, bool success, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("auth", "usp_Account_RecordLoginAttempt", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Success", success, SqlDbType.Bit)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask SetGradeAsync(string loginName, short accountGrade, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("auth", "usp_Account_SetGrade", 0)
            .AddParameter("LoginName", loginName, SqlDbType.NVarChar)
            .AddParameter("AccountGrade", accountGrade, SqlDbType.SmallInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
