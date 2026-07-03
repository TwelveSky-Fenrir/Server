using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Avatars;

/// <summary>
///     GameServer's counterpart to <c>Fenrir.Application.Login.Avatars.AvatarInfoFactory</c> -- same mapping from a
///     persisted character onto the shared <see cref="AvatarInfoTemplates.Zeroed" /> template, kept as a small
///     independent copy rather than a cross-Application-project reference (architecture reference §3.3: each
///     executable's application layer is independent). Feeds ZC_REGISTER_AVATAR_RECV's AVATAR_INFO payload.
/// </summary>
public static class AvatarInfoFactory
{
    /// <summary>Projects a persisted character onto the wire struct for ZC_REGISTER_AVATAR_RECV / world entry.</summary>
    public static AvatarInfo CreateForCharacter(CharacterWorldEntryDto character)
    {
        return AvatarInfoTemplates.Zeroed with
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
