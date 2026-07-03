using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Login.Avatars;

/// <summary>
///     Builds <see cref="AvatarInfo" /> (the ~190-field AVATAR_INFO payload embedded in
///     LC_CREATE_AVATAR_RECV/LC_USER_AVATAR_RECV2) from persisted character data. The wire-zero template itself
///     lives in <see cref="AvatarInfoTemplates" /> (Fenrir.Contracts) — shared with GameServer's own
///     ZC_REGISTER_AVATAR_RECV path, since Fenrir.Application.Game does not (and should not) reference this
///     project (architecture reference §3.3: each executable's application layer is independent). This class
///     only adds the LoginServer-specific mapping from a persisted character to that shared template.
/// </summary>
public static class AvatarInfoFactory
{
    /// <summary>Forwards to the shared template (kept here so existing call sites/tests need no changes).</summary>
    public static AvatarInfo Zeroed => AvatarInfoTemplates.Zeroed;

    /// <summary>Projects a persisted character onto the wire struct for LC_CREATE_AVATAR_RECV / world entry.</summary>
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
