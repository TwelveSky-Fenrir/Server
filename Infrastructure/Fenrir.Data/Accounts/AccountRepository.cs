using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Accounts;

/// <summary>
///     Singleton facade over auth.usp_Account_* (§11.1 of the architecture reference): no SqlDbType/builder ever
///     leaks past this type. AccountId is the legacy uUserIdx (ADR-0005) -- usp_Account_Create hands back the new
///     IDENTITY value directly, so callers never mint account ids client-side.
/// </summary>
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
}
