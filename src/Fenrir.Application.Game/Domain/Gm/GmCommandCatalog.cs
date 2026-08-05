using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public readonly record struct GmCommandDescriptor(
    int Sort,
    string Name,
    GmCommandTier RequiredTier,
    short AuditEventCode);

public static class GmCommandCatalog
{
    public const short MaxStatCheatAudit = 1;
    public const short PetExperienceGrantAudit = 2;
    public const short ExpGrantAudit = 3;
    public const short GrantMoneyAudit = 4;
    public const short FfaEventStartAudit = 5;
    public const short SummonMonsterAudit = 6;
    public const short MonsterForceKillAudit = 7;
    public const short CallAudit = 8;
    public const short KickAudit = 9;
    public const short ChatAudit = 10;
    public const short BlockAudit = 11;
    public const short CallPvpRelocateAudit = 12;
    public const short ClearInventoryAudit = 13;

    public const short HideAudit = 14;
    public const short ShowAudit = 15;
    public const short SelfTeleportAudit = 16;
    public const short TribeChangeAudit = 17;
    public const short EquipAudit = 18;
    public const short UnequipAudit = 19;
    public const short FindAudit = 20;
    public const short MoveToTargetAudit = 21;
    public const short TribeBankAudit = 22;
    public const short LevelSetAudit = 23;
    public const short StatEditAudit = 24;
    public const short MoveToPositionAudit = 25;
    public const short SetPvpPointAudit = 26;
    public const short CreateItemAudit = 27;

    public const short ChatCommandWhereAudit = 28;
    public const short ChatCommandYgDropAudit = 29;
    public const short ChatCommandBossAudit = 30;
    public const short ChatCommandKill200Audit = 31;
    public const short ChatCommandLabAudit = 32;
    public const short ChatCommandClearInventoryAudit = 33;

    public const short GlobalAnnouncementAudit = 34;
    public const short ChatCommandReloadAllAudit = 35;
    public const short ChatCommandReloadMonstersAudit = 36;
    public const short ChatCommandReloadItemsAudit = 37;
    public const short ChatCommandReloadQuestsAudit = 38;

    public const byte OutcomeDenied = 0;
    public const byte OutcomeExecuted = 1;
    public const byte OutcomeRejected = 2;

    private const string UnknownCommandName = "UNKNOWN";

    private static readonly FrozenDictionary<int, GmCommandDescriptor> Descriptors = Build();

    public static bool TryGet(int sort, out GmCommandDescriptor descriptor)
    {
        return Descriptors.TryGetValue(sort, out descriptor);
    }

    public static GmCommandDescriptor Resolve(int sort)
    {
        return Descriptors.TryGetValue(sort, out var descriptor)
            ? descriptor
            : new GmCommandDescriptor(sort, UnknownCommandName, GmCommandTier.Admin, 0);
    }

    private static FrozenDictionary<int, GmCommandDescriptor> Build()
    {
        GmCommandDescriptor[] descriptors =
        [
            new(333, "FFA-START", GmCommandTier.Elevated, FfaEventStartAudit),
            new(501, "HIDE", GmCommandTier.Basic, HideAudit),
            new(502, "SHOW", GmCommandTier.Basic, ShowAudit),
            new(503, "EXP", GmCommandTier.Elevated, ExpGrantAudit),
            new(504, "MONEY", GmCommandTier.Elevated, GrantMoneyAudit),
            new(505, "ITEM", GmCommandTier.Admin, CreateItemAudit),
            new(506, "MONCALL", GmCommandTier.Elevated, SummonMonsterAudit),
            new(507, "MOVE", GmCommandTier.Basic, SelfTeleportAudit),
            new(508, "DIE", GmCommandTier.Basic, MonsterForceKillAudit),
            new(509, "MAX", GmCommandTier.Admin, MaxStatCheatAudit),
            new(510, "TRIBE", GmCommandTier.Basic, TribeChangeAudit),
            new(511, "EQUIP", GmCommandTier.Basic, EquipAudit),
            new(512, "UNEQUIP", GmCommandTier.Basic, UnequipAudit),
            new(513, "FIND", GmCommandTier.Basic, FindAudit),
            new(514, "CALL", GmCommandTier.Basic, CallAudit),
            new(515, "UMOVE", GmCommandTier.Basic, MoveToTargetAudit),
            new(516, "NCHAT", GmCommandTier.Basic, ChatAudit),
            new(517, "YCHAT", GmCommandTier.Basic, ChatAudit),
            new(518, "KICK", GmCommandTier.Basic, KickAudit),
            new(519, "BLOCK", GmCommandTier.Basic, BlockAudit),
            new(520, "TRIBEBANK", GmCommandTier.Basic, TribeBankAudit),
            new(521, "LEVEL", GmCommandTier.Basic, LevelSetAudit),
            new(522, "STATEDIT", GmCommandTier.Basic, StatEditAudit),
            new(523, "ITEM-QUANTITY", GmCommandTier.Admin, CreateItemAudit),
            new(528, "MOVEPOS", GmCommandTier.Basic, MoveToPositionAudit),
            new(598, "SETPVPPOINT", GmCommandTier.Basic, SetPvpPointAudit),
            new(599, "CALLPVP", GmCommandTier.Basic, CallPvpRelocateAudit),
            new(700, "PETEXP", GmCommandTier.Admin, PetExperienceGrantAudit),
            new(701, "CLEARINVEN", GmCommandTier.Basic, ClearInventoryAudit)
        ];

        return descriptors.ToFrozenDictionary(static descriptor => descriptor.Sort);
    }
}
