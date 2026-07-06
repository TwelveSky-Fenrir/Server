using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2 -- creates a new character in the requested slot, grants the EU33 starter kit
///     (tribe equipment/inventory/skills/hotkeys, stats, pet, welcome buffs, one premium day) and returns its full
///     AVATAR_INFO payload.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:582-1183 -- USE_CUSTOME_CREATE branch. This macro is
///     force-defined unconditionally at S04_MyWork02.cpp:1 (before any other header, no matching #undef
///     anywhere under Server/, no ExcludedFromBuild condition in ts25login.vcxproj), bypassing the M33/
///     LNW33EU build-variant chain every other file in the codebase is subject to -- so the elite-gear/
///     weapon-remap/boosted-starting-level branch is what ships in every build configuration, not the
///     small-id branch a prior version of this comment assumed. See
///     Migrations/015_starter_kit_elite_grant.sql's header comment for the full citation ;
///     Server/Header/mapcheck.h:298-326 (GetReturnBornInTownLocation) ;
///     Server/ts25login/S04_MyWork02.cpp:625-662 (slot/name/tribe/head/face precondition sequence culminating in
///     the <see cref="AvatarNameValidator" /> whitelist call at l.658) ; Server/Header/safestring.h:43-81
///     (CheckNameString itself) ; Server/ts25login/S04_MyWork02.cpp:640-646 (the fourth-faction/Tribe-value-3
///     creation exclusion delegated to <see cref="Fenrir.Application.Login.Domain.Avatars.FourthFactionGate" />
///     via the service, see its own remarks for the full citation). PreviousTribe (the race/starter-kit-template
///     field) is deliberately NOT range-checked here: Server/ts25login/S04_MyWork02.cpp:739-838's PreviousTribe
///     switch has no case-3/default branch, so a value outside 0-2 is a genuine legacy validation gap -- the
///     request is not rejected on this basis, only the weapon-matching rule is skipped (see
///     <see cref="Fenrir.Application.Login.Services.CreateAvatar.CreateAvatarService" />'s own remarks).
/// </remarks>
public sealed class CreateAvatarHandler(ICreateAvatarService createAvatarService)
    : IAsyncPacketHandler<CreateAvatarRequest>
{
    public async ValueTask HandleAsync(CreateAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (packet.AvatarPost is < 0 or > 2 ||
            packet.AvatarName.Length == 0 ||
            packet.Tribe is < 0 or > 3 ||
            packet.Head is < 0 or > 6 ||
            packet.Face is < 0 or > 2)
        {
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        // CheckNameString: unlike the structural checks above, a whitelist violation here answers with a
        // normal failure response (client can retry with a different name) instead of disconnecting.
        if (!AvatarNameValidator.HasOnlyWhitelistedCharacters(packet.AvatarName))
        {
            session.Send(new CreateAvatarResponse { Result = 1, AvatarInfo = AvatarInfoFactory.Zeroed });
            return;
        }

        var result = await createAvatarService.CreateAvatarAsync(
            accountId,
            (byte)packet.AvatarPost,
            packet.AvatarName,
            (byte)packet.Tribe,
            (byte)packet.PreviousTribe,
            (byte)packet.Gender,
            (byte)packet.Head,
            (byte)packet.Face,
            packet.Weapon,
            cancellationToken);

        switch (result.Outcome)
        {
            // All three collapse to the same "malformed input, disconnect, no response" treatment the legacy
            // source gives every early field-validation failure in this handler -- see FourthFactionGate's own
            // remarks for why tribe value 3 lands here instead of getting a structured failure response, and
            // CreateAvatarOutcome.SlotOccupied's own remarks for the combined slot-occupied/name-empty test.
            case CreateAvatarOutcome.InvalidWeapon or CreateAvatarOutcome.FourthFactionDisabled
                or CreateAvatarOutcome.SlotOccupied:
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case CreateAvatarOutcome.Success:
                session.Send(new CreateAvatarResponse { Result = 0, AvatarInfo = result.AvatarInfo });
                return;
            case CreateAvatarOutcome.DominantTribeBlocked:
                // ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:250-264 (B_CREATE_AVATAR_RECV
                // Result=3): distinct from Result=1's generic-failure/name-taken/name-content collapse below,
                // per the dominant-tribe-gate contract's error semantics.
                session.Send(new CreateAvatarResponse { Result = 3, AvatarInfo = AvatarInfoFactory.Zeroed });
                return;
            default:
                // usp_Character_CreateWithStarterKit can still throw (e.g. name already taken by another account,
                // or a same-slot race lost against the proactive occupancy check above), but the wire contract only
                // documents Result=1 for any such failure -- the legacy client has no finer-grained handling.
                session.Send(new CreateAvatarResponse { Result = 1, AvatarInfo = AvatarInfoFactory.Zeroed });
                return;
        }
    }
}
