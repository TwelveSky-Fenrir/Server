using Fenrir.Application.Login.Abstractions.DeleteAvatar;
using Fenrir.Application.Login.Domain.Avatars;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.DeleteAvatar;

/// <summary>
///     Op18 CL_DELETE_AVATAR_SEND (delete branch) business logic: the three read-only refusals gating the
///     character delete -- tribe leadership/election role, guild membership, pending proxy-shop state -- run in
///     that fixed order, first failure wins, and only if none of them fire does the character actually get
///     deleted.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1237-1274 (sequential tribe-role/guild/proxy-shop checks;
///     ordering is significant -- the first failing check wins and later ones never run) ;
///     Server/Header/function.h:92-114 (ReturnTribeRole rank encoding) ;
///     Server/ts25login/S08_MyDB.cpp:563-576,638-671 (the legacy guild-membership field is a per-session cache
///     populated once at account login time, not re-queried at delete-request time -- read live here instead,
///     see remarks below) ; Server/ts25login/S08_MyDB.cpp:1109-1175 (CheckProxyShop) ;
///     Server/Header/H15_MyShare.h:6, Server/Header/S15_MyShare.cpp:103,153,158 (SHAREMEM_TRIBE_INFO -- a
///     cross-process shared-memory singleton read live by the legacy tribe-role check; read live here too, via
///     the same DB-backed ITribeRepository/IWorldStateRepository surface Game's own tribe features already use).
/// </remarks>
public sealed class DeleteAvatarService(
    ICharacterRepository characters,
    ITribeRepository tribes,
    IWorldStateRepository worldState,
    IGuildRepository guilds,
    IOfflineShopRepository offlineShops,
    ILogger<DeleteAvatarService> logger) : IDeleteAvatarService
{
    public async ValueTask<DeleteAvatarResult> DeleteAvatarAsync(int accountId, byte avatarPost,
        CancellationToken cancellationToken)
    {
        var roster = await characters.GetByAccountAsync(accountId, cancellationToken);
        var character = roster.FirstOrDefault(c => c.Slot == avatarPost);

        // No character in this slot: there is nothing to run the three business rules against. Legacy silently
        // disconnects on an out-of-range/empty slot instead (Server/ts25login/S04_MyWork02.cpp:1188-1213), but no
        // such upstream gate exists anywhere in Fenrir's wire path for this packet (DeleteAvatarRequest only
        // gates on session state). This is a deliberate, accepted divergence rather than an unverified
        // assumption: the delete call below is itself idempotent (usp_Character_Delete is a no-op on an
        // already-empty slot), so falling through to it here and answering with a normal Success response is
        // treated as intentional hardening (a graceful no-op instead of legacy's silent disconnect), not a gap.
        if (character is not null)
        {
            var outcome = await CheckBusinessRulesAsync(character, cancellationToken);
            if (outcome != DeleteAvatarOutcome.Success)
                return new DeleteAvatarResult(outcome);
        }

        try
        {
            await characters.DeleteAsync(accountId, avatarPost, cancellationToken);
        }
        catch (Exception ex)
        {
            // usp_Character_Delete faulting is a mapped, dedicated outcome (B_DELETE_AVATAR_RECV Result=1,
            // Server/ts25login/S04_MyWork02.cpp:1275-1283), not an unhandled fault: the caller gets a normal
            // failure response and the session survives, matching RenameAvatarService's equivalent SqlError
            // handling for the exact same class of failure.
            logger.LogError(ex, "Character delete failed for account {AccountId} slot {AvatarPost}", accountId,
                avatarPost);
            return new DeleteAvatarResult(DeleteAvatarOutcome.SqlError);
        }

        return new DeleteAvatarResult(DeleteAvatarOutcome.Success);
    }

    /// <summary>
    ///     Runs the three read-only refusal checks in their legacy-mandated order -- tribe role, then guild
    ///     membership, then proxy shop -- stopping at the first failure.
    /// </summary>
    private async ValueTask<DeleteAvatarOutcome> CheckBusinessRulesAsync(CharacterSummaryDto character,
        CancellationToken ct)
    {
        var tribeRole = await tribes.GetRoleForCharacterAsync(character.CharacterId, ct);
        var ownTribeVotes = await worldState.GetTribeVotesAsync(character.Tribe, ct);
        if (AvatarDeletionGate.TribeRoleBlocksDeletion(tribeRole, character.CharacterId, ownTribeVotes))
            return DeleteAvatarOutcome.TribeRoleRefusal;

        // Live read, not a login-time session cache: unlike the legacy session object, Login here keeps no
        // long-lived per-character avatar-info cache to reconcile at login time (see this class's own remarks).
        // A fresh read is always at least as correct as a stale cached one, and this decoupled repository path
        // costs no cross-process call either way -- both are plain DB reads from Login's own process.
        // Known, accepted divergence: legacy's snapshot is populated once at account login and never refreshed,
        // so a character that joins a guild mid-session and is deleted later in that same session is NOT refused
        // by legacy (its stale cache still reads "no guild"). This live read refuses it. Not a bug to "fix" toward
        // legacy's staleness -- flagged here only so the difference is a recorded, intentional choice.
        var guildMembership = await guilds.GetByCharacterAsync(character.CharacterId, ct);
        if (AvatarDeletionGate.GuildMembershipBlocksDeletion(guildMembership))
            return DeleteAvatarOutcome.GuildMembershipRefusal;

        var (shop, items) = await offlineShops.GetByCharacterAsync(character.CharacterId, ct);
        if (AvatarDeletionGate.ProxyShopBlocksDeletion(shop, items))
            return DeleteAvatarOutcome.ProxyShopRefusal;

        return DeleteAvatarOutcome.Success;
    }
}
