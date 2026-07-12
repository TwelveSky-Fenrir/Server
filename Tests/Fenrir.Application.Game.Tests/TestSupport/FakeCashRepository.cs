using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeCashRepository : ICashRepository
{
    private readonly Dictionary<int, int> _balances = new();

    public bool ThrowOnCredit { get; set; }
    public bool ThrowOnDebitAndGrantItem { get; set; }
    public bool ThrowOnCreditAndConsumeItem { get; set; }
    public bool ThrowOnGetBalance { get; set; }

    public (int AccountId, int Amount, byte Reason, int? ProductId)? LastCredit { get; private set; }

    public (int AccountId, int Amount, byte Reason, int ProductId, int CharacterId, byte Container,
        IReadOnlyList<CharacterItemSlotTvp> Items, int? AuditItemId, int? AuditQuantity, int? AuditSerial)?
        LastDebitAndGrantItem { get; private set; }

    public (int AccountId, int Amount, byte Reason, int? ProductId, int CharacterId, byte Container,
        IReadOnlyList<CharacterItemSlotTvp> Items)? LastCreditAndConsumeItem { get; private set; }

    public ValueTask<int> GetBalanceAsync(int accountId, CancellationToken ct)
    {
        if (ThrowOnGetBalance)
            throw new InvalidOperationException("Simulated SQL failure");

        return ValueTask.FromResult(_balances.GetValueOrDefault(accountId));
    }

    public ValueTask<int> DebitAndGrantItemAsync(int accountId, int amount, byte reason, int productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct,
        int? auditItemId = null, int? auditQuantity = null, int? auditSerial = null)
    {
        if (ThrowOnDebitAndGrantItem)
            throw new InvalidOperationException("Simulated SQL failure");

        LastDebitAndGrantItem = (accountId, amount, reason, productId, characterId, container, items,
            auditItemId, auditQuantity, auditSerial);
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

    public ValueTask<int> CreditAndConsumeItemAsync(int accountId, int amount, byte reason, int? productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        if (ThrowOnCreditAndConsumeItem)
            throw new InvalidOperationException("Simulated SQL failure");

        LastCreditAndConsumeItem = (accountId, amount, reason, productId, characterId, container, items);
        var newBalance = _balances.GetValueOrDefault(accountId) + amount;
        _balances[accountId] = newBalance;
        return ValueTask.FromResult(newBalance);
    }

    public void SeedBalance(int accountId, int balance)
    {
        _balances[accountId] = balance;
    }
}
