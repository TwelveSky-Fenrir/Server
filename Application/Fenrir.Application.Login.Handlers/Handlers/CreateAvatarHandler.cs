using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2 -- creates a new character in the requested slot at Level 1, grants a basic
///     weapon + torso-armor starter kit (tribe base inventory/skills/hotkeys, stats, welcome buffs -- see
///     <see cref="Fenrir.Application.Login.Services.CreateAvatar.CreateAvatarService" />'s own remarks for the
///     full character-creation-level1-redesign rationale) and returns its full AVATAR_INFO payload.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:582-1183 -- USE_CUSTOME_CREATE branch. This macro is
///     force-defined unconditionally at S04_MyWork02.cpp:1 (before any other header, no matching #undef
///     anywhere under Server/, no ExcludedFromBuild condition in ts25login.vcxproj), bypassing the M33/
///     LNW33EU build-variant chain every other file in the codebase is subject to -- so the weapon-remap
///     branch (tWeapon raw code -&gt; concrete ItemId, still honored below and by
///     <see cref="Fenrir.Application.Login.Services.CreateAvatar.CreateAvatarService.TryResolveWeaponItemId" />)
///     is what ships in every build configuration, not the small-id branch a prior version of this comment
///     assumed. The instant-elite-gear/boosted-starting-level portion of that same USE_CUSTOME_CREATE branch
///     is deliberately NOT replicated anymore -- see CreateAvatarService's own &lt;remarks&gt; for the
///     character-creation-level1-redesign rationale. Server/Header/mapcheck.h:298-326
///     (GetReturnBornInTownLocation) ;
///     Server/ts25login/S04_MyWork02.cpp:625-662 (slot/name/tribe/head/face precondition sequence culminating in
///     the <see cref="AvatarNameValidator" /> whitelist call at l.658) ; Server/Header/safestring.h:43-81
///     (CheckNameString itself) ; Server/ts25login/S04_MyWork02.cpp:640-646 (the fourth-faction/Tribe-value-3
///     creation exclusion delegated to <see cref="Fenrir.Application.Login.Domain.Avatars.FourthFactionGate" />
///     via the service, see its own remarks for the full citation). PreviousTribe (the race/starter-kit-template
///     field) IS range-checked here, to the same 0-2 domain the per-race switch actually recognizes --
///     Server/ts25login/S04_MyWork02.cpp:739-838's PreviousTribe switch has no case-3/default branch, so legacy
///     itself never rejects an out-of-range value on this basis, only the weapon-matching rule is skipped for
///     it (see <see cref="Fenrir.Application.Login.Services.CreateAvatar.CreateAvatarService" />'s own
///     remarks). This is a deliberate Fenrir-side divergence, not a missed legacy citation:
///     game.Characters.PreviousTribe carries a CK_Characters_PreviousTribe CHECK constraint enforcing that
///     exact 0-2 range (Migrations/018_character_previous_tribe_and_mount_readpath.sql:35-36), so an
///     out-of-range value that reached usp_Character_CreateWithStarterKit would fail there instead, on the
///     transaction's very first INSERT, as an uncaught, uncoded SqlException -- surfacing to the client as the
///     same generic CreateAvatarResponse{Result=1} any other undistinguished failure produces, not the clean
///     Malformed-disconnect a structurally-invalid field gets everywhere else on this request. Rejecting it
///     here instead closes that gap with the same treatment every other bounded field on this request already
///     gets (db-createwithstarterkit-fieldaudit finding).
///     <see cref="CreateAvatarRequest.Gender" /> gets the same 0-255 range check for a distinct reason:
///     legacy's AVATAR_INFO.aGender is a genuinely unclamped 32-bit int (Server/Header/Protocol/STRUCT.h:340,
///     748, 832 ; Server/Header/unity.h:43 ; Server/Header/Protocol/LOGIN.h:145 -- all `int aGender`,
///     confirmed by Server/BuildEU33/DB/nxtserver.sql:37's `aGender int(11) NULL DEFAULT 0` legacy DB column),
///     and Server/ts25login/S04_MyWork02.cpp:610,744 never bounds-checks it at all -- unlike tAvatarPost/
///     tTribe/tHead/tFace, which each get their own explicit range check a few lines apart in the same
///     function. Fenrir stores Gender in a TINYINT/byte (Database/Tables/game/Characters.sql:19), narrowed
///     from this packet's own wire-level `int Gender` via an unchecked `(byte)` cast below. Before this fix,
///     that cast was reachable with a fully out-of-range value (this field, uniquely among the six checked
///     here, was never included in the range test above), so an adversarial Gender outside 0-255 would wrap
///     silently (256 -&gt; 0, -1 -&gt; 255, 1000 -&gt; 232 via C#'s unchecked int-&gt;byte truncation) instead of
///     being rejected -- a genuinely different, silently-corrupted persisted value from what legacy's own
///     unclamped int column would have stored verbatim. The bound chosen (0-255) is the byte/TINYINT storage
///     width's own natural limit, not a guessed game-design-specific domain: no cited Server/ source documents
///     a narrower legal range (e.g. a male/female 0-1 split) for this field, so asserting one would be
///     inventing legacy behavior rather than reading it off the source -- see <see cref="CreateAvatarRequest" />'s
///     own remarks for the same point. Once Gender is provably in 0-255 here, the existing `(byte)` narrowing
///     below is lossless by construction, so no change to the TINYINT storage type itself was needed.
/// </remarks>
public sealed class CreateAvatarHandler(ICreateAvatarService createAvatarService, ILogger<CreateAvatarHandler> logger)
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
            packet.PreviousTribe is < 0 or > 2 ||
            packet.Gender is < 0 or > 255 ||
            packet.Head is < 0 or > 6 ||
            packet.Face is < 0 or > 2)
        {
            logger.LogWarning(
                "Create-avatar rejected: malformed request from account {AccountId} (slot {Slot}, tribe {Tribe}, " +
                "previousTribe {PreviousTribe})",
                accountId, packet.AvatarPost, packet.Tribe, packet.PreviousTribe);
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        // CheckNameString: unlike the structural checks above, a whitelist violation here answers with a
        // normal failure response (client can retry with a different name) instead of disconnecting.
        if (!AvatarNameValidator.HasOnlyWhitelistedCharacters(packet.AvatarName))
        {
            logger.LogWarning(
                "Create-avatar rejected: name fails whitelist check for account {AccountId} (slot {Slot})",
                accountId, packet.AvatarPost);
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
                logger.LogWarning(
                    "Create-avatar rejected: account {AccountId} slot {Slot} outcome {Outcome}", accountId,
                    packet.AvatarPost, result.Outcome);
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case CreateAvatarOutcome.Success:
                logger.LogInformation(
                    "Avatar created: account {AccountId} slot {Slot} name {AvatarName} tribe {Tribe}", accountId,
                    packet.AvatarPost, packet.AvatarName, packet.Tribe);
                session.Send(new CreateAvatarResponse { Result = 0, AvatarInfo = result.AvatarInfo });
                return;
            case CreateAvatarOutcome.DominantTribeBlocked:
                // ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:250-264 (B_CREATE_AVATAR_RECV
                // Result=3): distinct from Result=1's generic-failure/name-taken/name-content collapse below,
                // per the dominant-tribe-gate contract's error semantics.
                logger.LogWarning(
                    "Create-avatar rejected: tribe {Tribe} is currently dominant (account {AccountId})",
                    packet.Tribe, accountId);
                session.Send(new CreateAvatarResponse { Result = 3, AvatarInfo = AvatarInfoFactory.Zeroed });
                return;
            default:
                // usp_Character_CreateWithStarterKit can still throw (e.g. name already taken by another account,
                // or a same-slot race lost against the proactive occupancy check above), but the wire contract only
                // documents Result=1 for any such failure -- the legacy client has no finer-grained handling.
                logger.LogWarning(
                    "Create-avatar failed: account {AccountId} slot {Slot} (generic failure, see CreateAvatarService logs)",
                    accountId, packet.AvatarPost);
                session.Send(new CreateAvatarResponse { Result = 1, AvatarInfo = AvatarInfoFactory.Zeroed });
                return;
        }
    }
}
