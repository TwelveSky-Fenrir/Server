using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     Builds the AVATAR_INFO wire payload from a persisted character; zero-template shared with GameServer via
///     <see cref="AvatarInfoTemplates" />.
/// </summary>
public static class AvatarInfoFactory
{
    public static AvatarInfo Zeroed => AvatarInfoTemplates.Zeroed;

    public static AvatarInfo CreateForCharacter(CharacterWorldEntryDto character)
    {
        return Zeroed with
        {
            Name = character.Name,
            Tribe = character.Tribe,
            Gender = character.Gender,
            HeadType = character.HeadType,
            FaceType = character.FaceType,
            Level1 = character.Level,
            LogoutInfo =
            [
                character.MapId,
                (int)character.PosX,
                (int)character.PosY,
                (int)character.PosZ,
                character.Life,
                character.Mana
            ]
        };
    }

    // FEQUIP_TYPE (Server/Header/Protocol/STRUCT.h:1662-1676): amulet/cape/armor/gloves/ring/boots/an unused
    // slot/weapon/pet/4 decoration slots -- 13 slots total, each packed as 4 wire ints. Universal wire-shape
    // constants, not per-race data (bridge-stats-equipment contract, gap-table row 7); this is an independent
    // duplication of the same constants on GameServer's own AvatarInfoFactory, not a shared one, since Login
    // has no reference to the Game project.
    private const int EquipSlotCount = 13;
    private const int EquipWireIntsPerSlot = 4;

    /// <summary>
    ///     Projects the equipment rows CreateAvatarHandler is about to persist onto AVATAR_INFO's aEquip[13][4]
    ///     wire array -- independent re-implementation of GameServer's own
    ///     AvatarInfoFactory.BuildEquipArrayFromRows/PackUpgradeBytes (that one reads back CharacterItemSlotDto
    ///     rows from the DB; this one reads the CharacterItemSlotTvp rows the handler already holds in memory, one
    ///     request earlier in the same flow). The 4th int per slot is unmapped in both, left at wire-zero.
    /// </summary>
    public static int[] BuildEquipArray(IReadOnlyList<CharacterItemSlotTvp> equipment)
    {
        var equip = new int[EquipSlotCount * EquipWireIntsPerSlot];

        foreach (var item in equipment)
        {
            if (item.Slot >= EquipSlotCount)
                continue;

            var baseIndex = item.Slot * EquipWireIntsPerSlot;
            equip[baseIndex] = item.ItemId;
            equip[baseIndex + 1] = item.ExpireDate;
            equip[baseIndex + 2] = item.Enchant | (item.Combine << 8) | (item.Refine << 16) | (item.Socket << 24);
        }

        return equip;
    }
}
