using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Accounts;

/// <summary>
///     Mouse-PIN storage surface (plan decision D10: PIN hashed via <c>PasswordHasher</c>, never persisted in
///     clear). An interface — unlike the concrete-only <see cref="AccountRepository" /> — because the PIN state
///     machine (create/verify/change, 3-strikes, PinRequired→CharSelect) is unit-tested in
///     Fenrir.Application.Login.Tests without a SQL container, and CaeriusNet's query surface is extension-method
///     based, so a fake must sit at this seam.
/// </summary>
public interface IAccountPinRepository
{
    /// <summary>The stored PIN credential, or null when the account has no PIN yet (op 13 create path).</summary>
    public ValueTask<AccountPinDto?> GetAsync(int accountId, CancellationToken ct);

    /// <summary>
    ///     Creates or replaces the PIN credential (auth.usp_AccountPin_Set is an upsert, like the legacy
    ///     UpdateMousePassword).
    /// </summary>
    public ValueTask SetAsync(int accountId, byte[] pinHash, byte[] pinSalt, CancellationToken ct);
}

/// <summary>
///     Singleton facade over auth.usp_AccountPin_* -- same shape as <see cref="AccountRepository" />: no
///     SqlDbType/builder ever leaks past this type, procs only.
/// </summary>
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
