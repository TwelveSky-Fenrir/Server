using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Accounts;

namespace Fenrir.Data.Accounts;

public sealed record AccountPinRepository(ICaeriusNetDbContext Db) : IAccountPinRepository
{
    public async ValueTask<AccountPinDto?> GetAsync(int accountId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("auth", "usp_AccountPin_Get", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<AccountPinDto>(sp, ct);
    }

    public async ValueTask SetAsync(int accountId, byte[] pinHash, byte[] pinSalt, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("auth", "usp_AccountPin_Set", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("PinHash", pinHash, SqlDbType.VarBinary)
            .AddParameter("PinSalt", pinSalt, SqlDbType.VarBinary)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask RecordAttemptAsync(int accountId, bool success, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("auth", "usp_AccountPin_RecordAttempt", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Success", success, SqlDbType.Bit)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
