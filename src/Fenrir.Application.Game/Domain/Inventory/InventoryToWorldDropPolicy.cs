using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class InventoryToWorldDropPolicy
{
    public enum GroundDropReshapeOutcome
    {
        Reshaped,
        RejectedUnhandledSort,
        RejectedQuantityRange,
        RejectedPackedValueRange
    }

    public enum Outcome
    {
        SourceOutOfRange,
        PremiumPageExpired,
        UnknownItem,
        NonDroppableItem,
        QuantityOutOfRange,
        InsufficientQuantity,

        UnsupportedItemType,
        InvalidPackedValue,
        GroundItemTableFull,

        Success
    }

    public const int GroundItemCapacity = 4000;


    public const long MaxNumberSentinel = 2_000_000_000L;

    public const float MonsterMoneyGroundReductionRatio = 0.15f;

    public static bool IsPremiumPageAccessAllowed(long premiumExpireUnixSeconds, long nowUnixSeconds)
    {
        return premiumExpireUnixSeconds >= nowUnixSeconds;
    }

    public static Result Resolve(
        int sourcePage,
        int sourceSlot,
        int requestedQuantity,
        bool premiumPageAccessAllowed,
        ItemStack? source,
        ItemDefinition? itemDefinition,
        bool sourceIsDroppableByPlayer,
        Func<ItemDefinition, GroundItemSpawnEligibility> evaluateSpawnEligibility,
        int currentGroundItemCount,
        float dropperPosX, float dropperPosY, float dropperPosZ,
        string dropperName, string? dropperPartyName)
    {
        if (sourcePage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)sourcePage, sourceSlot))
            return Fail(Outcome.SourceOutOfRange);

        if (sourcePage == ContainerMatrix.InventoryPage1 && !premiumPageAccessAllowed)
            return Fail(Outcome.PremiumPageExpired);

        if (source is not { } src)
            return Fail(Outcome.UnknownItem);

        if (itemDefinition is null)
            return Fail(Outcome.UnknownItem);

        if (!sourceIsDroppableByPlayer)
            return Fail(Outcome.NonDroppableItem);

        var itemSort = itemDefinition.Item.Sort;
        var isStackable = ContainerMatrix.IsStackableSort(itemSort);

        if (!TryResolveDropQuantity(itemSort, src.Quantity, requestedQuantity, out var groundQuantity))
            return Fail(Outcome.QuantityOutOfRange);

        if (isStackable && groundQuantity > src.Quantity)
            return Fail(Outcome.InsufficientQuantity);

        var eligibility = evaluateSpawnEligibility(itemDefinition);
        if (eligibility == GroundItemSpawnEligibility.UnsupportedItemType)
            return Fail(Outcome.UnsupportedItemType);
        if (eligibility == GroundItemSpawnEligibility.InvalidPackedValue)
            return Fail(Outcome.InvalidPackedValue);

        if (currentGroundItemCount >= GroundItemCapacity)
            return Fail(Outcome.GroundItemTableFull);

        var master = string.IsNullOrEmpty(dropperPartyName) ? dropperName : dropperPartyName;

        if (isStackable)
        {
            var spawn = new GroundItemSpawnPlan(itemDefinition.Item.ItemId, groundQuantity, 0, 0, 0, 0, 0,
                dropperPosX, dropperPosY, dropperPosZ, master, "",
                GroundItemEntity.ManualGroundDropSort);

            var remaining = src.Quantity - groundQuantity;
            var newSource = remaining > 0 ? src with { Quantity = remaining } : (ItemStack?)null;
            return new Result(Outcome.Success, newSource, spawn);
        }

        var value = ItemValueCodec.Encode(src.Enchant, src.Combine, src.Refine, src.Socket);
        var uniqueSpawn = new GroundItemSpawnPlan(itemDefinition.Item.ItemId, groundQuantity, value, src.Serial,
            src.SocketGem1, src.SocketGem2, src.SocketGem3, dropperPosX, dropperPosY, dropperPosZ, master, "",
            GroundItemEntity.ManualGroundDropSort);

        return new Result(Outcome.Success, null, uniqueSpawn);
    }

    private static Result Fail(Outcome outcome)
    {
        return new Result(outcome, null, null);
    }

    private static bool TryResolveDropQuantity(byte itemSort, int sourceQuantity, int requestedQuantity,
        out int groundQuantity)
    {
        groundQuantity = 0;

        if (requestedQuantity <= 0)
            return false;

        if (ContainerMatrix.IsStackableSort(itemSort))
        {
            if (sourceQuantity is < ItemQuantityPolicy.MinStackQuantity or > ItemQuantityPolicy.MaxStackQuantity ||
                requestedQuantity > ItemQuantityPolicy.MaxStackQuantity)
                return false;

            groundQuantity = requestedQuantity;
            return true;
        }

        if (requestedQuantity != 1)
            return false;

        switch (itemSort)
        {
            case ItemQuantityPolicy.PetSort when sourceQuantity is >= ItemQuantityPolicy.MinStackQuantity and <=
                ItemQuantityPolicy.MaxPetActivity:
                groundQuantity = sourceQuantity;
                return true;

            case not ItemQuantityPolicy.PetSort when ItemQuantityPolicy.CarriesNoQuantity(itemSort) &&
                                                     sourceQuantity is 0 or 1:
                groundQuantity = 1;
                return true;

            default:
                return false;
        }
    }

    public static GroundDropReshape ReshapeGroundDrop(byte itemSort, int dropSort, int incomingQuantity,
        int incomingValue, float ownerTowerSilverRatio = 0f)
    {
        switch (itemSort)
        {
            case 1:
                return ReshapeMoney(dropSort, incomingQuantity, ownerTowerSilverRatio);

            case 2 or 99:
            {
                if (incomingQuantity < ItemQuantityPolicy.MinStackQuantity ||
                    incomingQuantity > GroundItemPickupPolicy.MaxStackQuantity)
                    return GroundDropReshape.Reject(GroundDropReshapeOutcome.RejectedQuantityRange);

                return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, incomingQuantity, 0, 0);
            }

            case 3 or 4 or 5 or 6:
                return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, 1, 0, 0);

            case >= 7 and <= 21:
                return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, 1, incomingValue, 0);

            case ItemQuantityPolicy.PetSort:
            {
                if (incomingQuantity < ItemQuantityPolicy.MinStackQuantity ||
                    incomingQuantity > ItemQuantityPolicy.MaxPetActivity)
                    return GroundDropReshape.Reject(GroundDropReshapeOutcome.RejectedQuantityRange);

                if (incomingValue < 0 || incomingValue > MaxNumberSentinel)
                    return GroundDropReshape.Reject(GroundDropReshapeOutcome.RejectedPackedValueRange);

                return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, incomingQuantity, incomingValue, 0);
            }

            case >= 23 and <= 33:
                return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, 1, incomingValue, 0);

            default:
                return GroundDropReshape.Reject(GroundDropReshapeOutcome.RejectedUnhandledSort);
        }
    }

    private static GroundDropReshape ReshapeMoney(int dropSort, int incomingQuantity, float ownerTowerSilverRatio)
    {
        long ground = incomingQuantity;
        long deposit = 0;

        if (dropSort == GroundItemEntity.MonsterKillDropSort)
        {
            ground -= (long)(ground * MonsterMoneyGroundReductionRatio);
            if (ground > 0)
                deposit = ground;

            ground += (long)(ground * ownerTowerSilverRatio);
        }

        if (ground < 1 || ground > MaxNumberSentinel)
            return GroundDropReshape.Reject(GroundDropReshapeOutcome.RejectedQuantityRange);

        return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, (int)ground, 0, deposit);
    }

    public readonly record struct Result(Outcome Outcome, ItemStack? NewSource, GroundItemSpawnPlan? Spawn)
    {
        public bool IsMalformed => Outcome is Outcome.SourceOutOfRange or Outcome.PremiumPageExpired
            or Outcome.UnknownItem or Outcome.NonDroppableItem or Outcome.QuantityOutOfRange
            or Outcome.InsufficientQuantity;

        public bool IsSoftFailure => Outcome is Outcome.UnsupportedItemType or Outcome.InvalidPackedValue
            or Outcome.GroundItemTableFull;

        public bool Succeeded => Outcome == Outcome.Success;
    }

    public readonly record struct GroundDropReshape(
        GroundDropReshapeOutcome Outcome,
        int Quantity,
        int Value,
        long TribeBankDepositAmount)
    {
        public bool Reshaped => Outcome == GroundDropReshapeOutcome.Reshaped;

        internal static GroundDropReshape Reject(GroundDropReshapeOutcome outcome)
        {
            return new GroundDropReshape(outcome, 0, 0, 0);
        }
    }
}

public enum GroundDropOrigin
{
    MonsterToWorld,
    PvpToWorld,
    InventoryToWorld,
    GmToWorld,
    BuyTribeItemToWorld,
    Zone175ToWorld,
    Zone194ToWorld,
    Zone200ToWorld,
    CpExchangeToWorld,
    LevelBonusToWorld,
    TreasureChestToWorld,
    TreasureChest175ToWorld,
    InventoryCrossTransferToWorld
}

public readonly record struct EliteDropNotice(int Type, byte Tribe, int Value, string AvatarName)
{
    public const int TypeTreasureChest = 55;

    public const int TypeTreasureChest175 = 56;

    public const int TypeMonster = 0;

    public const int TypeCpExchange = 1;

    public const int TypePvp = 2;
}

public static class EliteDropNoticeResolver
{
    public static EliteDropNotice? Resolve(GroundDropOrigin origin, int itemId, byte ownerTribe, string ownerName)
    {
        var type = origin switch
        {
            GroundDropOrigin.TreasureChestToWorld => EliteDropNotice.TypeTreasureChest,
            GroundDropOrigin.TreasureChest175ToWorld => EliteDropNotice.TypeTreasureChest175,
            GroundDropOrigin.MonsterToWorld => EliteDropNotice.TypeMonster,
            GroundDropOrigin.CpExchangeToWorld => EliteDropNotice.TypeCpExchange,
            GroundDropOrigin.PvpToWorld => EliteDropNotice.TypePvp,
            _ => (int?)null
        };

        if (type is not { } typeCode || !ShouldShowName(origin, itemId))
            return null;

        return new EliteDropNotice(typeCode, ownerTribe, itemId, ownerName);
    }

    private static bool ShouldShowName(GroundDropOrigin origin, int itemId)
    {
        if (itemId is >= 1002 and <= 1005)
            return origin is GroundDropOrigin.TreasureChestToWorld or GroundDropOrigin.TreasureChest175ToWorld;

        return false;
    }
}
