using Fenrir.Application.Login.Abstractions.RenameAvatar;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Application.Login.Services.AccountSecurity;

namespace Fenrir.Application.Login.Services.RenameAvatar;

/// <summary>
///     Op19 CL_CHANGE_AVATAR_NAME_SEND business logic: the rename-scroll item gate, then the five read-only
///     relationship refusals (tribe role, guild, friend, teacher, student, in that fixed order, first failure
///     wins), and only if none of them fire, the atomic rename-scroll consumption + rename itself. A
///     successful rename is additionally recorded as a game.EventLog AccountSecurity row -- a new Fenrir
///     observability addition with no legacy analog, not a reproduced legacy behavior; every refusal/failure
///     path writes nothing, since no state actually changed.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1340-1349 (item-1133 gate and new-name charset
///     re-validation -- the charset half is already enforced upstream by RenameAvatarHandler via
///     AvatarNameValidator, before this service is ever called) ; Server/ts25login/S04_MyWork02.cpp:1358-1385
///     (the five relationship refusals, see AvatarRenameGate for the per-rule citations) ;
///     Server/ts25login/S04_MyWork02.cpp:1386-1401 and Server/ts25login/S08_MyDB.cpp:848-899 (the legacy
///     in-memory mutation + non-transactional DB write + manual, incomplete compensation on failure -- see
///     ICharacterRenameRepository.RenameAndConsumeItemAsync's own remarks for why Fenrir's single-transaction
///     stored procedure makes that documented socket-field data-loss bug structurally impossible instead of
///     reproducing it). Slot-occupancy (a character existing at <paramref name="avatarPost" /> at all) is one
///     of this contract's out-of-scope preconditions; unlike op18's DeleteAvatarService (which lets an empty
///     slot fall through to an idempotent delete), a rename with nothing to rename simply reports SlotMissing.
/// </remarks>
public sealed class RenameAvatarService(
    ICharacterRepository characters,
    ITribeRepository tribes,
    IWorldStateRepository worldState,
    IGuildRepository guilds,
    IFriendRepository friends,
    IMentorRepository mentors,
    ICharacterRenameRepository renames,
    IEventLogRepository eventLog) : IRenameAvatarService
{
    public async ValueTask<RenameAvatarResult> RenameAvatarAsync(int accountId, byte avatarPost,
        string changeAvatarName, byte itemContainer, byte itemSlot, CancellationToken cancellationToken)
    {
        var roster = await characters.GetByAccountAsync(accountId, cancellationToken);
        var character = roster.FirstOrDefault(c => c.Slot == avatarPost);
        if (character is null)
            return new RenameAvatarResult(RenameAvatarOutcome.SlotMissing);

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
        catch (Exception)
        {
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
