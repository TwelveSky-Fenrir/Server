using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class KillFeedLeaderboardTests
{
    [Fact]
    public void RecordKill_NewCharacter_AddsEntry()
    {
        var board = new KillFeedLeaderboard();

        Assert.True(board.RecordKill(1, "Alice", tribe: 0, killTotal: 1));

        var top3 = board.GetTopThree();
        Assert.Single(top3);
        Assert.Equal(1, top3[0].CharacterId);
        Assert.Equal("Alice", top3[0].Name);
        Assert.Equal(1, top3[0].Kills);
    }

    [Fact]
    public void RecordKill_SameCharacterAgain_UpdatesInPlace_DoesNotDuplicate()
    {
        var board = new KillFeedLeaderboard();

        board.RecordKill(1, "Alice", 0, 1);
        board.RecordKill(1, "Alice", 0, 2);

        Assert.Equal(1, board.Count);
        Assert.Equal(2, board.GetTopThree()[0].Kills);
    }

    [Fact]
    public void GetTopThree_OrdersDescendingByKills()
    {
        var board = new KillFeedLeaderboard();

        board.RecordKill(1, "Alice", 0, 3);
        board.RecordKill(2, "Bob", 1, 7);
        board.RecordKill(3, "Carol", 2, 5);

        var top3 = board.GetTopThree();

        Assert.Equal(3, top3.Length);
        Assert.Equal(2, top3[0].CharacterId); // Bob, 7 kills
        Assert.Equal(3, top3[1].CharacterId); // Carol, 5 kills
        Assert.Equal(1, top3[2].CharacterId); // Alice, 3 kills
    }

    [Fact]
    public void GetTopThree_MoreThanThreeTrackedKillers_ReturnsOnlyTopThree()
    {
        var board = new KillFeedLeaderboard();

        for (var i = 1; i <= 5; i++)
            board.RecordKill(i, $"Killer{i}", 0, i);

        var top3 = board.GetTopThree();

        Assert.Equal(3, top3.Length);
        Assert.Equal(5, top3[0].Kills);
        Assert.Equal(4, top3[1].Kills);
        Assert.Equal(3, top3[2].Kills);
    }

    [Fact]
    public void GetTopThree_FewerThanThreeTrackedKillers_ReturnsOnlyThoseTracked()
    {
        var board = new KillFeedLeaderboard();
        board.RecordKill(1, "Alice", 0, 1);

        Assert.Single(board.GetTopThree());
    }

    [Fact]
    public void GetTopThree_NoKillsYet_ReturnsEmpty()
    {
        var board = new KillFeedLeaderboard();

        Assert.Empty(board.GetTopThree());
    }

    [Fact]
    public void RecordKill_CapacityFull_NewDistinctCharacter_SilentlyUntracked()
    {
        var board = new KillFeedLeaderboard();

        for (var i = 0; i < KillFeedLeaderboard.Capacity; i++)
            Assert.True(board.RecordKill(i, $"Killer{i}", 0, 1));

        Assert.Equal(KillFeedLeaderboard.Capacity, board.Count);

        // The 1001st distinct killer is silently untracked -- no exception, no state change.
        Assert.False(board.RecordKill(KillFeedLeaderboard.Capacity, "Overflow", 0, 999));
        Assert.Equal(KillFeedLeaderboard.Capacity, board.Count);
    }

    [Fact]
    public void RecordKill_CapacityFull_ExistingCharacterCanStillUpdate()
    {
        var board = new KillFeedLeaderboard();

        for (var i = 0; i < KillFeedLeaderboard.Capacity; i++)
            board.RecordKill(i, $"Killer{i}", 0, 1);

        Assert.True(board.RecordKill(0, "Killer0", 0, 50));
        Assert.Equal(50, board.GetTopThree()[0].Kills);
    }

    [Fact]
    public void Clear_RemovesEveryEntry()
    {
        var board = new KillFeedLeaderboard();
        board.RecordKill(1, "Alice", 0, 5);
        board.RecordKill(2, "Bob", 1, 10);

        board.Clear();

        Assert.Equal(0, board.Count);
        Assert.Empty(board.GetTopThree());
    }
}
