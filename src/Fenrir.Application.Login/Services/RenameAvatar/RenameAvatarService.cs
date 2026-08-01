using Fenrir.Application.Login.Abstractions.RenameAvatar;
using Fenrir.Application.Login.Services.AccountSecurity;
using Fenrir.Domain.Login.Avatars;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.RenameAvatar;

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
    private const int InventoryPageCount = 2;
    private const int InventorySlotCount = 64;

    public async ValueTask<RenameAvatarResult> RenameAvatarAsync(int accountId, byte avatarPost,
        string changeAvatarName, int itemContainer, int itemSlot, CancellationToken cancellationToken)
    {
        var roster = await characters.GetByAccountAsync(accountId, cancellationToken);
        var character = roster.FirstOrDefault(c => c.Slot == avatarPost);
        if (character is null || character.Name.Length == 0)
            return new RenameAvatarResult(RenameAvatarOutcome.SlotEmpty);

        // The "same name" answer is emitted BEFORE the page/index bounds and before the charset filter
        // (Server/ts25login/S04_MyWork02.cpp:1313-1337): those bounds must not turn it into a disconnect.
        if (string.Equals(changeAvatarName, character.Name, StringComparison.OrdinalIgnoreCase))
            return new RenameAvatarResult(RenameAvatarOutcome.NameTaken);

        if (itemContainer is < 0 or >= InventoryPageCount || itemSlot is < 0 or >= InventorySlotCount)
            return new RenameAvatarResult(RenameAvatarOutcome.Malformed);

        var itemIdAtSlot = await characters.GetItemIdAtSlotAsync(character.CharacterId, (byte)itemContainer,
            (byte)itemSlot, cancellationToken);
        if (!AvatarRenameGate.ItemAtSlotIsRenameScroll(itemIdAtSlot))
            return new RenameAvatarResult(RenameAvatarOutcome.ItemMismatch);

        if (!AvatarNameValidator.HasOnlyWhitelistedCharacters(changeAvatarName))
            return new RenameAvatarResult(RenameAvatarOutcome.Malformed);

        var relationshipRefusal = await CheckRelationshipRefusalsAsync(character, cancellationToken);
        if (relationshipRefusal is { } outcome)
            return new RenameAvatarResult(outcome);

        int code;
        try
        {
            code = await renames.RenameAndConsumeItemAsync(accountId, avatarPost, changeAvatarName,
                (byte)itemContainer, (byte)itemSlot, cancellationToken);
        }
        catch (Exception ex)
        {
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
                return new RenameAvatarResult(RenameAvatarOutcome.ItemMismatch);
            default:
                return new RenameAvatarResult(RenameAvatarOutcome.SlotMissing);
        }
    }

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
