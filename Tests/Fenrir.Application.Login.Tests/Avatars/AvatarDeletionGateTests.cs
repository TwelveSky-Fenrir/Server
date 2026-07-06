using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Login.Tests.Avatars;

// Op18 CL_DELETE_AVATAR_SEND (delete branch)'s three read-only business-rule refusals. Réf. C++ :
// Server/ts25login/S04_MyWork02.cpp:1237-1274 ; Server/Header/function.h:92-114 (ReturnTribeRole).
public class AvatarDeletionGateTests
{
    [Fact]
    public void TribeRoleBlocksDeletion_RegularMemberNotCandidate_NeverBlocks()
    {
        Assert.False(AvatarDeletionGate.TribeRoleBlocksDeletion(0, 100, []));
    }

    [Theory]
    [InlineData((byte)1)] // tribe master
    [InlineData((byte)2)] // tribe sub-master
    public void TribeRoleBlocksDeletion_MasterOrSubMaster_Blocks(byte role)
    {
        Assert.True(AvatarDeletionGate.TribeRoleBlocksDeletion(role, 100, []));
    }

    [Fact]
    public void TribeRoleBlocksDeletion_RegisteredVoteCandidateForOwnTribe_Blocks()
    {
        // Rank 3 (active election candidate) has no dedicated ReturnTribeRole return value in Fenrir's own
        // usp_TribeRole_GetForCharacter (deliberately not resolved there); it collapses to the same refusal via
        // a separate TribeVotes lookup instead -- ServerDocs/12_ts25zone/04_MyWork02_PartieA.md:498-502.
        var votes = new[]
        {
            new TribeVoteDto(0, 0, 100, 50, 0, 10, DateTime.UtcNow),
            new TribeVoteDto(0, 1, 200, 60, 0, 5, DateTime.UtcNow)
        };

        Assert.True(AvatarDeletionGate.TribeRoleBlocksDeletion(0, 100, votes));
        Assert.False(AvatarDeletionGate.TribeRoleBlocksDeletion(0, 999, votes));
    }

    [Fact]
    public void GuildMembershipBlocksDeletion_NoMembership_Allowed()
    {
        Assert.False(AvatarDeletionGate.GuildMembershipBlocksDeletion(null));
    }

    [Fact]
    public void GuildMembershipBlocksDeletion_AnyMembership_Blocks()
    {
        var membership = new CharacterGuildMembershipDto(1, "Guild", 0, "Rookie");

        Assert.True(AvatarDeletionGate.GuildMembershipBlocksDeletion(membership));
    }

    [Fact]
    public void ProxyShopBlocksDeletion_NoShopRowAtAll_Allowed()
    {
        Assert.False(AvatarDeletionGate.ProxyShopBlocksDeletion(null, []));
    }

    [Theory]
    [InlineData(1, 0, 0)] // nonzero ShopState
    [InlineData(0, 1, 0)] // nonzero Money
    [InlineData(0, 0, 1)] // nonzero BigMoney
    public void ProxyShopBlocksDeletion_NonzeroStateOrMoney_BlocksRegardlessOfItems(byte state, int money,
        int bigMoney)
    {
        var shop = new OfflineShopRowDto(1, null, state, 0, money, bigMoney, 0, 0, 0, "");

        Assert.True(AvatarDeletionGate.ProxyShopBlocksDeletion(shop, []));
    }

    [Fact]
    public void ProxyShopBlocksDeletion_ZeroStateAndMoneyNoItemRows_Allowed()
    {
        var shop = new OfflineShopRowDto(1, null, 0, 0, 0, 0, 0, 0, 0, "");

        Assert.False(AvatarDeletionGate.ProxyShopBlocksDeletion(shop, []));
    }

    [Fact]
    public void ProxyShopBlocksDeletion_ZeroStateAndMoneyButItemRowsRemain_Blocks()
    {
        var shop = new OfflineShopRowDto(1, null, 0, 0, 0, 0, 0, 0, 0, "");
        var items = new[] { new OfflineShopItemRowDto(0, 1234, 1, 100, 1, 500, null) };

        Assert.True(AvatarDeletionGate.ProxyShopBlocksDeletion(shop, items));
    }
}
