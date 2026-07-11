using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.GenericAction;

public enum GenericActionStatus
{

        Aborted,

        Failed,

        Succeeded
}

public readonly record struct GenericActionResult(
    GenericActionStatus Status,
    bool NotifyQuestProgress = false,
    int? GrantedPetExperienceGrowth = null)
{
    public static readonly GenericActionResult Aborted = new(GenericActionStatus.Aborted);
    public static readonly GenericActionResult Failed = new(GenericActionStatus.Failed);
    public static readonly GenericActionResult Succeeded = new(GenericActionStatus.Succeeded);
}

public interface IGenericActionService
{
    public ValueTask<GenericActionResult> MoveContainerAsync(int sort, byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

        public ValueTask<GenericActionResult> PickupGroundItemAsync(byte[] data, Zone zone, PlayerRuntimeState state,
        int accountId, int characterId, CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> PayTeleportTollAsync(byte[] data, int characterId,
        CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> LearnSkillAsync(int sort, byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> UpgradeSkillAsync(byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

        public ValueTask<GenericActionResult> SellToNpcShopAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, DefaultPData move, CancellationToken cancellationToken);

        public ValueTask<GenericActionResult> BuyFromNpcShopAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, DefaultPData move, CancellationToken cancellationToken);

        public ValueTask<GenericActionResult> AllocateStatPointAsync(int statSort, int addValue, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);

        public ValueTask<GenericActionResult> TimeExchangeAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, CancellationToken cancellationToken);

        public ValueTask<GenericActionResult> TransferStoreItemAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken);

        public ValueTask<GenericActionResult> TransferStoreMoneyAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken);

        public ValueTask<GenericActionResult> TransferBankItemAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken);

        public ValueTask<GenericActionResult> TransferBankMoneyAsync(int sort, byte[] data, int accountId,
        int characterId, CancellationToken cancellationToken);
}
