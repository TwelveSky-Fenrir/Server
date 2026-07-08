using Fenrir.Application.Login.Abstractions.RenameAvatar;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Application.Login.Services.AccountSecurity;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.RenameAvatar;

/// <summary>
///     Op19 CL_CHANGE_AVATAR_NAME_SEND business logic: first, an identical-name short circuit (the requested
///     name exactly matches the character's current name), then the rename-scroll item gate, then the five
///     read-only relationship refusals (tribe role, guild, friend, teacher, student, in that fixed order,
///     first failure wins), and only if none of them fire, the atomic rename-scroll consumption + rename
///     itself. A successful rename is additionally recorded as a game.EventLog AccountSecurity row -- a new
///     Fenrir observability addition with no legacy analog, not a reproduced legacy behavior; every
///     refusal/failure path writes nothing, since no state actually changed.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1325-1329 (the identical-name short circuit runs first,
///     before the item check, the charset re-validation, and all five relationship refusals -- so a same-name
///     request from a relationship-blocked character still gets the "name unchanged" code and never touches
///     the item slot at all; reproduced here as the very first check in this method, ahead of the item gate)
///     ; Server/ts25login/S04_MyWork02.cpp:1340-1349 (item-1133 gate and new-name charset re-validation --
///     the charset half is already enforced upstream by RenameAvatarHandler via AvatarNameValidator, before
///     this service is ever called) ; Server/ts25login/S04_MyWork02.cpp:1358-1385 (the five relationship
///     refusals, see AvatarRenameGate for the per-rule citations) ; Server/ts25login/S04_MyWork02.cpp:1386-1401
///     and Server/ts25login/S08_MyDB.cpp:848-899 (the legacy in-memory mutation + non-transactional DB write +
///     manual, incomplete compensation on failure -- see ICharacterRenameRepository.RenameAndConsumeItemAsync's
///     own remarks for why Fenrir's single-transaction stored procedure makes that documented socket-field
///     data-loss bug structurally impossible instead of reproducing it). Slot-occupancy (a character existing
///     at <paramref name="avatarPost" /> at all) has no upstream gate anywhere in Fenrir's wire path for this
///     packet -- unlike legacy's silent disconnect within the same slot/name/page/index precondition sequence
///     RenameAvatarHandler's own remarks already cite (Server/ts25login/S04_MyWork02.cpp:1310-1349) -- this is
///     a deliberate, accepted divergence: a rename with nothing to rename reports SlotMissing (Result=102) as
///     a graceful response instead of disconnecting, the same documented posture op18's DeleteAvatarService
///     takes (there, an empty slot instead falls through to an idempotent delete, since delete has no
///     analogous "nothing changed" outcome to report).
/// </remarks>
public sealed class RenameAvatarService(
    ICharacterRepository characters,
    ITribeRepository tribes,
    IWorldStateRepository worldState,
    IGuildRepository guilds,
    IFriendRepository friends,
    IMentorRepository mentors,
    ICharacterRenameRepository renames,
    IEventLogRepository eventLog,
    ILogger<RenameAvatarService> logger) : IRenameAvatarService
{
    public async ValueTask<RenameAvatarResult> RenameAvatarAsync(int accountId, byte avatarPost,
        string changeAvatarName, byte itemContainer, byte itemSlot, CancellationToken cancellationToken)
    {
        var roster = await characters.GetByAccountAsync(accountId, cancellationToken);
        var character = roster.FirstOrDefault(c => c.Slot == avatarPost);
        if (character is null)
            return new RenameAvatarResult(RenameAvatarOutcome.SlotMissing);

        // Identical-name short circuit, run before anything else -- matching legacy's own ordering (this
        // class's own remarks). A request that asks for the name the character already has is answered with
        // the same "name unchanged" code the underlying uniqueness check would eventually produce (it does not
        // exclude self, see usp_Character_Rename.sql's own header), but without ever touching the rename-scroll
        // item slot or running any relationship refusal -- so a same-name request never costs the caller an
        // item and is never masked by a relationship refusal the character happens to also be subject to.
        // Case-insensitive: names are unique case-insensitively under this schema's default collation (no
        // explicit COLLATE on game.Characters.Name), so a pure case change is "no change" by the same rule the
        // uniqueness check itself would apply.
        if (string.Equals(changeAvatarName, character.Name, StringComparison.OrdinalIgnoreCase))
            return new RenameAvatarResult(RenameAvatarOutcome.NameTaken);

        var itemIdAtSlot =
            await characters.GetItemIdAtSlotAsync(character.CharacterId, itemContainer, itemSlot, cancellationToken);
        if (!AvatarRenameGate.ItemAtSlotIsRenameScroll(itemIdAtSlot))
            return new RenameAvatarResult(RenameAvatarOutcome.ItemMismatch);

        var relationshipRefusal = await CheckRelationshipRefusalsAsync(character, cancellationToken);
        if (relationshipRefusal is { } outcome)
            return new RenameAvatarResult(outcome);

        int code;
        try
        {
            code = await renames.RenameAndConsumeItemAsync(accountId, avatarPost, changeAvatarName, itemContainer,
                itemSlot, cancellationToken);
        }
        catch (Exception ex)
        {
            // Previously swallowed with no trace at all -- logged here (the only place the exception itself is
            // still in scope) so a real rename failure is diagnosable instead of vanishing silently, matching
            // DeleteAvatarService/CreateAvatarService's own equivalent catch blocks.
            logger.LogError(ex, "Character rename failed for account {AccountId} slot {AvatarPost}", accountId,
                avatarPost);
            return new RenameAvatarResult(RenameAvatarOutcome.SqlError);
        }

        switch (code)
        {
            case 0:
                await eventLog.LogAsync(AccountSecurityEventCodes.AvatarRenamed, EventLogCategory.AccountSecurity,
                    accountId, character.CharacterId, null, null, null, null, null, null, null, null,
                    $"Slot={avatarPost};OldName={character.Name};NewName={changeAvatarName}", cancellationToken);
                return new RenameAvatarResult(RenameAvatarOutcome.Success);
            case 2:
                return new RenameAvatarResult(RenameAvatarOutcome.NameTaken);
            case -1:
                // TOCTOU race: the item this method already verified moments earlier no longer matches by the
                // time the atomic proc ran. Treated identically to the earlier item-gate failure.
                return new RenameAvatarResult(RenameAvatarOutcome.ItemMismatch);
            default:
                // 102, or any other code the proc might one day return -- collapses to SlotMissing, the same
                // "affected zero rows for some other reason" bucket the original single-purpose proc used.
                return new RenameAvatarResult(RenameAvatarOutcome.SlotMissing);
        }
    }

    /// <summary>
    ///     Runs the five read-only refusal checks in their legacy-mandated order -- tribe role, guild, friend,
    ///     teacher, student -- stopping at the first failure. Returns null if none of them fire.
    /// </summary>
    private async ValueTask<RenameAvatarOutcome?> CheckRelationshipRefusalsAsync(CharacterSummaryDto character,
        CancellationToken ct)
    {
        var tribeRole = await tribes.GetRoleForCharacterAsync(character.CharacterId, ct);
        var ownTribeVotes = await worldState.GetTribeVotesAsync(character.Tribe, ct);
        if (AvatarRenameGate.TribeRoleBlocksRename(tribeRole, character.CharacterId, ownTribeVotes))
            return RenameAvatarOutcome.TribeRoleRefusal;

        var guildMembership = await guilds.GetByCharacterAsync(character.CharacterId, ct);
        if (AvatarRenameGate.GuildMembershipBlocksRename(guildMembership))
            return RenameAvatarOutcome.GuildMembershipRefusal;

        var friendList = await friends.GetByCharacterAsync(character.CharacterId, ct);
        if (AvatarRenameGate.FriendListBlocksRename(friendList))
            return RenameAvatarOutcome.FriendListRefusal;

        var mentor = await mentors.GetForCharacterAsync(character.CharacterId, ct);
        if (AvatarRenameGate.TeacherBondBlocksRename(mentor))
            return RenameAvatarOutcome.TeacherBondRefusal;

        if (AvatarRenameGate.StudentBondBlocksRename(mentor))
            return RenameAvatarOutcome.StudentBondRefusal;

        return null;
    }
}
