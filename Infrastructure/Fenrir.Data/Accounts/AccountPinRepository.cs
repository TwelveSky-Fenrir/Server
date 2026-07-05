using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Accounts;

// Interface (unlike AccountRepository) so the PIN state machine can be unit-tested via a fake, without a SQL container.
public interface IAccountPinRepository
{
    /// <summary>Null when the account has no PIN yet.</summary>
    public ValueTask<AccountPinDto?> GetAsync(int accountId, CancellationToken ct);

    /// <summary>Upsert (auth.usp_AccountPin_Set), mirrors the legacy UpdateMousePassword.</summary>
    public ValueTask SetAsync(int accountId, byte[] pinHash, byte[] pinSalt, CancellationToken ct);
}

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
}
