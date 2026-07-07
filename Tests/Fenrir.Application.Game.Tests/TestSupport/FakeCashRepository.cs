using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for ICashRepository: tracks per-account balance and the last credit/debit call so
///     handler test suites can assert on both without a real SQL Server 2025 instance.
/// </summary>
internal sealed class FakeCashRepository : ICashRepository
{
    private readonly Dictionary<int, int> _balances = new();

    public bool ThrowOnCredit { get; set; }
    public bool ThrowOnDebitAndGrantItem { get; set; }
    public bool ThrowOnGetBalance { get; set; }

    public (int AccountId, int Amount, byte Reason, int? ProductId)? LastCredit { get; private set; }

    public (int AccountId, int Amount, byte Reason, int ProductId, int CharacterId, byte Container,
        IReadOnlyList<CharacterItemSlotTvp> Items)? LastDebitAndGrantItem { get; private set; }

    public ValueTask<int> GetBalanceAsync(int accountId, CancellationToken ct)
    {
        if (ThrowOnGetBalance)
            throw new InvalidOperationException("Simulated SQL failure");

        return ValueTask.FromResult(_balances.GetValueOrDefault(accountId));
    }

    public ValueTask<int> DebitAndGrantItemAsync(int accountId, int amount, byte reason, int productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        if (ThrowOnDebitAndGrantItem)
            throw new InvalidOperationException("Simulated SQL failure");

        LastDebitAndGrantItem = (accountId, amount, reason, productId, characterId, container, items);
        var newBalance = _balances.GetValueOrDefault(accountId) - amount;
        _balances[accountId] = newBalance;
        return ValueTask.FromResult(newBalance);
    }

    public ValueTask CreditAsync(int accountId, int amount, byte reason, int? productId, CancellationToken ct)
    {
        if (ThrowOnCredit)
            throw new InvalidOperationException("Simulated SQL failure");

        LastCredit = (accountId, amount, reason, productId);
        _balances[accountId] = _balances.GetValueOrDefault(accountId) + amount;
        return ValueTask.CompletedTask;
    }

    public void SeedBalance(int accountId, int balance)
    {
        _balances[accountId] = balance;
    }
}
