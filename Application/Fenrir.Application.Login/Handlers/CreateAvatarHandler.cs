using Fenrir.Application.Login.Avatars;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2 -- creates a new character in the requested slot and returns its full
///     AVATAR_INFO payload. Reachable from <see cref="LoginSessionState.Authenticated" /> or
///     <see cref="LoginSessionState.CharSelect" /> (see the packet's <c>AllowedStates</c>), both of which imply
///     <c>MarkAuthenticated</c> already ran, so <see cref="LoginClientSession.AccountId" /> is always set here.
/// </summary>
public sealed class CreateAvatarHandler(ICharacterRepository characters)
    : IAsyncPacketHandler<CreateAvatarRequest>
{
    // M1 has no per-tribe spawn/stat system (gameplay is out of scope for this milestone), so every new character
    // starts at the same coordinates/vitals as the Phase 4 seed account (Database/70_seed/001_dev_account.sql).
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

        // packet.Weapon has no matching column in game.Characters (Phase 4 schema is M1-minimal, no starting
        // weapon/inventory system yet) -- dropped deliberately, not an oversight.
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

            // Guaranteed non-null: we just created this row and nothing else can delete it within this request.
            var character = await characters.GetForWorldEntryAsync(characterId, cancellationToken);

            session.Send(new CreateAvatarResponse
            {
                Result = 0,
                AvatarInfo = AvatarInfoFactory.CreateForCharacter(character!)
            });
        }
        catch (Exception)
        {
            // usp_Character_Create THROWs distinct codes for "slot occupied" vs "name taken" (50201/50202), but
            // the wire contract only documents Result=1 ("creation failed") for the client beyond Result=0 --
            // there is no client-side handling for a finer-grained code, so every creation failure collapses to
            // the same generic Result here rather than inventing an undocumented code the legacy client ignores.
            session.Send(new CreateAvatarResponse
            {
                Result = 1,
                AvatarInfo = AvatarInfoFactory.Zeroed
            });
        }
    }
}
