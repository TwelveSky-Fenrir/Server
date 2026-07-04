using Fenrir.Application.Login.Avatars;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2 -- creates a new character in the requested slot and returns its full AVATAR_INFO
///     payload.
/// </summary>
public sealed class CreateAvatarHandler(ICharacterRepository characters)
    : IAsyncPacketHandler<CreateAvatarRequest>
{
    // M1 has no per-tribe spawn/stat system: every new character starts at the same coordinates/vitals as the seed account.
    private const short StartMapId = 1;
    private const float StartPosX = 0;
    private const float StartPosY = 0;
    private const float StartPosZ = 0;
    private const int StartLife = 100;
    private const int StartMaxLife = 100;
    private const int StartMana = 50;
    private const int StartMaxMana = 50;

    public async ValueTask HandleAsync(CreateAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        // packet.Weapon has no matching column in game.Characters (no starting weapon/inventory system yet) -- dropped deliberately.
        try
        {
            var characterId = await characters.CreateAsync(
                accountId,
                (byte)packet.AvatarPost,
                packet.AvatarName,
                (byte)packet.Tribe,
                (byte)packet.Gender,
                (byte)packet.Head,
                (byte)packet.Face,
                StartMapId,
                StartPosX,
                StartPosY,
                StartPosZ,
                StartLife,
                StartMaxLife,
                StartMana,
                StartMaxMana,
                cancellationToken);

            // Guaranteed non-null: we just created this row within this request.
            var character = await characters.GetForWorldEntryAsync(characterId, cancellationToken);

            session.Send(new CreateAvatarResponse
            {
                Result = 0,
                AvatarInfo = AvatarInfoFactory.CreateForCharacter(character!)
            });
        }
        catch (Exception)
        {
            // usp_Character_Create throws distinct codes (slot occupied/name taken), but the wire contract only
            // documents Result=1 for any failure -- the legacy client has no finer-grained handling.
            session.Send(new CreateAvatarResponse
            {
                Result = 1,
                AvatarInfo = AvatarInfoFactory.Zeroed
            });
        }
    }
}
