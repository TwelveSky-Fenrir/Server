using Fenrir.Application.Login.Abstractions.DeleteAvatar;
using Fenrir.Application.Login.Services.DeleteAvatar;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Services;

// Op18 CL_DELETE_AVATAR_SEND (delete branch): the three read-only business-rule refusals, run in a fixed order
// (tribe role, then guild, then proxy shop), first failure wins. Réf. C++ :
// Server/ts25login/S04_MyWork02.cpp:1237-1274.
public class DeleteAvatarServiceTests
{
    private const int AccountId = 42;
    private const int CharacterId = 1000;
    private const byte Slot = 0;
    private const byte Tribe = 1;

    private static CharacterSummaryDto Summary => new(CharacterId, Slot, "Hero", Tribe, 0, 0, 0, 10);

    [Fact]
    public async Task DeleteAvatarAsync_NoRefusalsApply_DeletesAndReturnsSuccess()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var service = new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
            FakeWorldStateRepository.Empty(), FakeGuildRepository.Empty(), FakeOfflineShopRepository.Empty(),
            NullLogger<DeleteAvatarService>.Instance);

        var result = await service.DeleteAvatarAsync(AccountId, Slot, CancellationToken.None);

        Assert.Equal(DeleteAvatarOutcome.Success, result.Outcome);
        Assert.Single(characters.DeleteCalls);
        Assert.Equal((AccountId, Slot), characters.DeleteCalls[0]);
    }

    [Fact]
    public async Task DeleteAvatarAsync_EmptySlot_SkipsBusinessRulesAndDeletesIdempotently()
    {
        var characters = FakeCharacterRepository.WithNone();
        var guilds = FakeGuildRepository.Empty();
        var shops = FakeOfflineShopRepository.Empty();
        var service = new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
            FakeWorldStateRepository.Empty(), guilds, shops, NullLogger<DeleteAvatarService>.Instance);

        var result = await service.DeleteAvatarAsync(AccountId, Slot, CancellationToken.None);

        Assert.Equal(DeleteAvatarOutcome.Success, result.Outcome);
        Assert.Single(characters.DeleteCalls);
        Assert.Empty(guilds.QueriedCharacterIds);
        Assert.Empty(shops.QueriedCharacterIds);
    }

    [Theory]
    [InlineData((byte)1)] // tribe master
    [InlineData((byte)2)] // tribe sub-master
    public async Task DeleteAvatarAsync_TribeMasterOrSubMaster_RefusesWithoutDeletingOrQueryingLaterChecks(
        byte role)
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var guilds = FakeGuildRepository.Empty();
        var shops = FakeOfflineShopRepository.Empty();
        var service = new DeleteAvatarService(characters, FakeTribeRepository.WithRole(CharacterId, role),
            FakeWorldStateRepository.Empty(), guilds, shops, NullLogger<DeleteAvatarService>.Instance);

        var result = await service.DeleteAvatarAsync(AccountId, Slot, CancellationToken.None);

        Assert.Equal(DeleteAvatarOutcome.TribeRoleRefusal, result.Outcome);
        Assert.Empty(characters.DeleteCalls);
        Assert.Empty(guilds.QueriedCharacterIds);
        Assert.Empty(shops.QueriedCharacterIds);
    }

    [Fact]
    public async Task DeleteAvatarAsync_RegisteredTribeVoteCandidate_RefusesAsTribeRole()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var votes = new[] { new TribeVoteDto(Tribe, 0, CharacterId, 50, 0, 10, DateTime.UtcNow) };
        var service = new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
            FakeWorldStateRepository.WithVotes(votes), FakeGuildRepository.Empty(),
            FakeOfflineShopRepository.Empty(), NullLogger<DeleteAvatarService>.Instance);

        var result = await service.DeleteAvatarAsync(AccountId, Slot, CancellationToken.None);

        Assert.Equal(DeleteAvatarOutcome.TribeRoleRefusal, result.Outcome);
        Assert.Empty(characters.DeleteCalls);
    }

    [Fact]
    public async Task DeleteAvatarAsync_GuildMember_RefusesWithoutDeletingOrQueryingShop()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var shops = FakeOfflineShopRepository.Empty();
        var membership = new CharacterGuildMembershipDto(1, "Guild", 0, "Rookie");
        var service = new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
            FakeWorldStateRepository.Empty(), FakeGuildRepository.WithMembership(CharacterId, membership), shops,
            NullLogger<DeleteAvatarService>.Instance);

        var result = await service.DeleteAvatarAsync(AccountId, Slot, CancellationToken.None);

        Assert.Equal(DeleteAvatarOutcome.GuildMembershipRefusal, result.Outcome);
        Assert.Empty(characters.DeleteCalls);
        Assert.Empty(shops.QueriedCharacterIds);
    }

    [Fact]
    public async Task DeleteAvatarAsync_PendingProxyShop_Refuses()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var shop = new OfflineShopRowDto(CharacterId, null, 1, 0, 0, 0, 0, 0, 0, "");
        var service = new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
            FakeWorldStateRepository.Empty(), FakeGuildRepository.Empty(),
            FakeOfflineShopRepository.With(CharacterId, shop), NullLogger<DeleteAvatarService>.Instance);

        var result = await service.DeleteAvatarAsync(AccountId, Slot, CancellationToken.None);

        Assert.Equal(DeleteAvatarOutcome.ProxyShopRefusal, result.Outcome);
        Assert.Empty(characters.DeleteCalls);
    }

    [Fact]
    public async Task DeleteAvatarAsync_TribeAndGuildBothApply_TribeRoleWinsBecauseItRunsFirst()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var membership = new CharacterGuildMembershipDto(1, "Guild", 0, "Rookie");
        var guilds = FakeGuildRepository.WithMembership(CharacterId, membership);
        var service = new DeleteAvatarService(characters, FakeTribeRepository.WithRole(CharacterId, 1),
            FakeWorldStateRepository.Empty(), guilds, FakeOfflineShopRepository.Empty(),
            NullLogger<DeleteAvatarService>.Instance);

        var result = await service.DeleteAvatarAsync(AccountId, Slot, CancellationToken.None);

        Assert.Equal(DeleteAvatarOutcome.TribeRoleRefusal, result.Outcome);
        // The guild check never runs once the tribe-role check has already failed.
        Assert.Empty(guilds.QueriedCharacterIds);
    }
}
