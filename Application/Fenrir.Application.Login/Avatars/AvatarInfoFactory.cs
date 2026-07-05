using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Login.Avatars;

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

    /// <summary>
    ///     Projects the equipment rows CreateAvatarHandler is about to persist onto AVATAR_INFO's aEquip[13][4]
    ///     wire array -- independent re-implementation of GameServer's own
    ///     AvatarInfoFactory.BuildEquipArrayFromRows/PackUpgradeBytes (that one reads back CharacterItemSlotDto
    ///     rows from the DB; this one reads the CharacterItemSlotTvp rows the handler already holds in memory, one
    ///     request earlier in the same flow). The 4th int per slot is unmapped in both, left at wire-zero.
    /// </summary>
    public static int[] BuildEquipArray(IReadOnlyList<CharacterItemSlotTvp> equipment)
    {
        var equip = new int[52];

        foreach (var item in equipment)
        {
            if (item.Slot >= 13)
                continue;

            var baseIndex = item.Slot * 4;
            equip[baseIndex] = item.ItemId;
            equip[baseIndex + 1] = item.ExpireDate;
            equip[baseIndex + 2] = item.Enchant | (item.Combine << 8) | (item.Refine << 16) | (item.Socket << 24);
        }

        return equip;
    }
}
