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

        // No character in this slot: there is nothing to run the three business rules against. Slot-occupancy
        // is one of this contract's out-of-scope preconditions (already gated earlier in the legacy handler);
        // the delete call below is itself idempotent (usp_Character_Delete is a no-op on an already-empty slot),
        // so falling through to it here is safe rather than an error.
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
            // Previously invisible: usp_Character_Delete failing here left no trace anywhere (see
            // DeleteAvatarHandler's own remarks on the "1=DB delete failure, not modeled here" gap) -- logged for
            // observability only, behavior/semantics unchanged (still rethrows, caller still sees an unhandled fault).
            logger.LogError(ex, "Character delete failed for account {AccountId} slot {AvatarPost}", accountId,
                avatarPost);
            throw;
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
        var guildMembership = await guilds.GetByCharacterAsync(character.CharacterId, ct);
        if (AvatarDeletionGate.GuildMembershipBlocksDeletion(guildMembership))
            return DeleteAvatarOutcome.GuildMembershipRefusal;

        var (shop, items) = await offlineShops.GetByCharacterAsync(character.CharacterId, ct);
        if (AvatarDeletionGate.ProxyShopBlocksDeletion(shop, items))
            return DeleteAvatarOutcome.ProxyShopRefusal;

        return DeleteAvatarOutcome.Success;
    }
}
