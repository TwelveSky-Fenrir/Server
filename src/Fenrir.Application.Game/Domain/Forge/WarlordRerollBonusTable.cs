using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Forge;

public static class WarlordRerollBonusTable
{
    public enum WarlordRerollOutcome
    {
        NoCandidate,

        Top,

        Mid,

        Base
    }

    private const int RareTribeStride = 21;

    private const int EliteTribeStride = 22;

    private const byte IAmulet = 7;
    private const byte IArmor = 9;
    private const byte IGlove = 10;
    private const byte IRing = 11;
    private const byte IBoots = 12;
    private const byte ISword = 13;
    private const byte IBlade = 14;
    private const byte IMarble = 15;
    private const byte IKatana = 16;
    private const byte IDblade = 17;
    private const byte ILute = 18;
    private const byte ILblade = 19;
    private const byte ISpear = 20;
    private const byte IScepter = 21;

    private static readonly FrozenDictionary<byte, SlotMapping> SlotMappings =
        new Dictionary<byte, SlotMapping>
        {
            [IAmulet] = new(WarlordSlotGroup.Amulet, 0, false),
            [IRing] = new(WarlordSlotGroup.Ring, 0, false),
            [IArmor] = new(WarlordSlotGroup.Armor, 0, false),
            [IGlove] = new(WarlordSlotGroup.Gloves, 0, false),
            [IBoots] = new(WarlordSlotGroup.Boots, 0, false),
            [ISword] = new(WarlordSlotGroup.SwordFamily, 0, true),
            [IBlade] = new(WarlordSlotGroup.SwordFamily, 1, true),
            [IMarble] = new(WarlordSlotGroup.SwordFamily, 2, true),
            [IKatana] = new(WarlordSlotGroup.KatanaFamily, 0, true),
            [IDblade] = new(WarlordSlotGroup.KatanaFamily, 1, true),
            [ILute] = new(WarlordSlotGroup.KatanaFamily, 2, true),
            [ILblade] = new(WarlordSlotGroup.LongBladeFamily, 0, true),
            [ISpear] = new(WarlordSlotGroup.LongBladeFamily, 1, true),
            [IScepter] = new(WarlordSlotGroup.LongBladeFamily, 2, true)
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<WarlordSlotGroup, BonusRow> RareRows =
        new Dictionary<WarlordSlotGroup, BonusRow>
        {
            [WarlordSlotGroup.Amulet] = new(87020, 5, null, 0, 87001),
            [WarlordSlotGroup.Ring] = new(87019, 5, null, 0, 87000),
            [WarlordSlotGroup.Armor] = new(87016, 5, 87006, 41, 87005),
            [WarlordSlotGroup.Gloves] = new(87017, 5, null, 0, 87007),
            [WarlordSlotGroup.Boots] = new(87018, 5, 87012, 16, 87008),
            [WarlordSlotGroup.SwordFamily] = new(87013, 5, 87009, 16, 87002),
            [WarlordSlotGroup.KatanaFamily] = new(87034, 5, 87030, 16, 87023),
            [WarlordSlotGroup.LongBladeFamily] = new(87055, 5, 87051, 16, 87044)
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<WarlordSlotGroup, BonusRow> EliteRows =
        new Dictionary<WarlordSlotGroup, BonusRow>
        {
            [WarlordSlotGroup.Amulet] = new(87084, 1, null, 0, 87064),
            [WarlordSlotGroup.Ring] = new(87083, 1, null, 0, 87063),
            [WarlordSlotGroup.Armor] = new(87080, 1, 87074, 15, 87068),
            [WarlordSlotGroup.Gloves] = new(87081, 1, 87075, 15, 87069),
            [WarlordSlotGroup.Boots] = new(87082, 1, 87076, 15, 87070),
            [WarlordSlotGroup.SwordFamily] = new(87077, 1, 87071, 15, 87065),
            [WarlordSlotGroup.KatanaFamily] = new(87099, 1, 87093, 15, 87087),
            [WarlordSlotGroup.LongBladeFamily] = new(87121, 1, 87115, 15, 87109)
        }.ToFrozenDictionary();

    public static WarlordRerollDrawResult TryDrawReplacement(
        byte sort, byte itemType, byte previousTribe, WarlordPityLockState pityLock, IRandomSource random)
    {
        if (itemType is not (RankChangeResolver.RareItemType or RankChangeResolver.EliteItemType))
            return NoCandidate();

        if (previousTribe > 2)
            return NoCandidate();

        if (!SlotMappings.TryGetValue(sort, out var mapping))
            return NoCandidate();

        var isElite = itemType == RankChangeResolver.EliteItemType;
        var rows = isElite ? EliteRows : RareRows;
        if (!rows.TryGetValue(mapping.Group, out var row))
            return NoCandidate();

        var draw = pityLock.Engaged ? WarlordPityLockState.ForcedDrawValue : random.NextInt32(100);

        var offset = mapping.UsesFamilyOffset
            ? mapping.FamilyOffset
            : previousTribe * (isElite ? EliteTribeStride : RareTribeStride);

        if (draw < row.TopPercent)
        {
            var setsPityLock = isElite;
            if (setsPityLock)
                pityLock.Engage();

            return new WarlordRerollDrawResult(WarlordRerollOutcome.Top, row.TopId + offset, setsPityLock, true);
        }

        if (row.MidId is { } midId && draw < row.TopPercent + row.MidPercent)
            return new WarlordRerollDrawResult(WarlordRerollOutcome.Mid, midId + offset, false, false);

        return new WarlordRerollDrawResult(WarlordRerollOutcome.Base, row.BaseId + offset, false, false);
    }

    public static bool NoticeReachesRecipients(byte itemType)
    {
        return itemType == RankChangeResolver.EliteItemType;
    }

    private static WarlordRerollDrawResult NoCandidate()
    {
        return new WarlordRerollDrawResult(WarlordRerollOutcome.NoCandidate, 0, false, false);
    }

    private enum WarlordSlotGroup : byte
    {
        Amulet,
        Ring,
        Armor,
        Gloves,
        Boots,
        SwordFamily,
        KatanaFamily,
        LongBladeFamily
    }

    private readonly record struct SlotMapping(WarlordSlotGroup Group, int FamilyOffset, bool UsesFamilyOffset);

    private readonly record struct BonusRow(int TopId, int TopPercent, int? MidId, int MidPercent, int BaseId);

    public readonly record struct WarlordRerollDrawResult(
        WarlordRerollOutcome Outcome,
        int ReplacementItemId,
        bool SetsPityLock,
        bool IsTopTierOutcome);
}
