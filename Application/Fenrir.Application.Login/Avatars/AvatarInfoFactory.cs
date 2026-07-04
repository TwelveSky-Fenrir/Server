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
}
