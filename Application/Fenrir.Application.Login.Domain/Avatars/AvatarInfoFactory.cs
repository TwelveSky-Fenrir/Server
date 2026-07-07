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

    // FEQUIP_TYPE::EPET, index 8 -- same independent-duplication posture as EquipSlotCount/EquipWireIntsPerSlot
    // above (Game's own copy lives at Fenrir.Application.Game.Domain.Pets.PetSlots.EquipmentSlot).
    private const int PetEquipSlot = 8;

    /// <summary>
    ///     Projects the equipment rows CreateAvatarHandler is about to persist onto AVATAR_INFO's aEquip[13][4]
    ///     wire array -- independent re-implementation of GameServer's own
    ///     AvatarInfoFactory.BuildEquipArrayFromRows/PackUpgradeBytes (that one reads back CharacterItemSlotDto
    ///     rows from the DB; this one reads the CharacterItemSlotTvp rows the handler already holds in memory, one
    ///     request earlier in the same flow). The 4th int per slot is unmapped in both, left at wire-zero.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1131-1135 -- the pet slot's 2nd/3rd wire ints are the
    ///     pet's activity/growth as plain values (NOT the expiration-date/packed-Enchant-Combine-Refine-Socket
    ///     pair every other slot carries there), since those two values live on the character record itself
    ///     rather than on the pet's own CharacterItems row (<paramref name="petGrowth" />/<paramref name="petActivity" />
    ///     come from the caller's already-known creation-time literals, not from <paramref name="equipment" />'s
    ///     own zeroed Enchant/Combine/Refine/Socket/ExpireDate fields for that row). Defaults to 0/0 so callers
    ///     with no pet in scope (e.g. existing unit tests) are unaffected.
    /// </remarks>
    public static int[] BuildEquipArray(IReadOnlyList<CharacterItemSlotTvp> equipment, int petGrowth = 0,
        byte petActivity = 0)
    {
        var equip = new int[EquipSlotCount * EquipWireIntsPerSlot];

        foreach (var item in equipment)
        {
            if (item.Slot >= EquipSlotCount)
                continue;

            var baseIndex = item.Slot * EquipWireIntsPerSlot;
            equip[baseIndex] = item.ItemId;

            if (item.Slot == PetEquipSlot)
            {
                equip[baseIndex + 1] = petActivity;
                equip[baseIndex + 2] = petGrowth;
            }
            else
            {
                equip[baseIndex + 1] = item.ExpireDate;
                equip[baseIndex + 2] = item.Enchant | (item.Combine << 8) | (item.Refine << 16) | (item.Socket << 24);
            }
        }

        return equip;
    }
}
