using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="PendingSocialRequestAutoCancelSystem" />: per-tick pending-social-request auto-cancel
///     across all 5 kinds (behavior contract C21§G). Uses a real <see cref="ZoneRegistry" /> (not a lone
///     <see cref="Zone" />) because the system's own "counterpart still reachable" check is shard-wide, via
///     <see cref="ZoneRegistry.TryGetPlayer" />, matching every one of the 5 registries' own process-wide
///     scope.
/// </summary>
public class PendingSocialRequestAutoCancelSystemTests
{
    private static (Zone Zone, TradeRegistry Trade, FriendRegistry Friend, MentorRegistry Mentor,
        PartyRegistry Party, GuildInviteRegistry Guild) SetUp(short mapId)
    {
        var options = ZoneTestKit.Options();
        var optionsWrapper = Options.Create(options);
        var movementRules = new MovementRules(optionsWrapper);
        var dirtyTracker = new DirtyTracker<int>();
        var worldData = ZoneTestKit.EmptyWorldData();

        var trade = new TradeRegistry();
        var friend = new FriendRegistry();
        var mentor = new MentorRegistry();
        var party = new PartyRegistry();
        var guild = new GuildInviteRegistry();

        // Lazy<ZoneRegistry> circular-init: the system needs a back-reference to the very ZoneRegistry that
        // is constructing it (same "Lazy<T> breaks the constructor-graph cycle" pattern this system's own
        // remarks document for the real DI container) -- captured by reference, assigned only once the
        // registry itself is fully constructed, resolved lazily well after that (inside Simulate).
        ZoneRegistry? registryRef = null;
        var zoneRegistryLazy = new Lazy<ZoneRegistry>(() => registryRef!);
        var system = new PendingSocialRequestAutoCancelSystem(trade, friend, mentor, party, guild, zoneRegistryLazy);

        var registry = new ZoneRegistry(optionsWrapper, movementRules, dirtyTracker, NullLogger<Zone>.Instance,
            worldData, [system]);
        registryRef = registry;
        registry.Initialize([mapId]);

        return (registry[mapId], trade, friend, mentor, party, guild);
    }

    private static void Enter(Zone zone, int characterId, string name)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId, name: name)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void Trade_AskerOnline_TargetNeverOnline_PendingClearedOnTheNextSweep()
    {
        var (zone, trade, _, _, _, _) = SetUp(37);
        Enter(zone, 1, "Alice");

        Assert.Equal(TradeAskOutcome.Sent, trade.TryAsk(1, 2)); // character 2 never enters any zone
        Assert.True(trade.IsBusy(1));

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.False(trade.IsBusy(1));
    }

    [Fact]
    public void Trade_BothPartiesOnline_PendingSurvivesTheSweep()
    {
        var (zone, trade, _, _, _, _) = SetUp(37);
        Enter(zone, 1, "Alice");
        Enter(zone, 2, "Bob");

        trade.TryAsk(1, 2);

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.True(trade.IsBusy(1));
        Assert.True(trade.IsBusy(2));
    }

    [Fact]
    public void Trade_ActiveSession_NeverAutoCancelled_OnlyThePendingAskStateIsSwept()
    {
        // An actually-started TradeSession (state "4", mid-trade confirmed) is a DIFFERENT precondition from
        // this system's own "pending, awaiting response" scope (Precondition G's own note) -- must never be
        // touched by this sweep, even once its counterpart later disconnects.
        var (zone, trade, _, _, _, _) = SetUp(37);
        Enter(zone, 1, "Alice");

        trade.TryAsk(1, 2);
        trade.TryAnswer(2, true, out _);
        Assert.True(trade.TryStart(1, out _));

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.True(trade.TryGetSession(1, out _));
    }

    [Fact]
    public void Friend_TargetOnline_AskerNeverOnline_PendingClearedViaSilentDecline()
    {
        var (zone, _, friend, _, _, _) = SetUp(37);
        Enter(zone, 2, "Bob");

        Assert.Equal(FriendAskOutcome.Sent, friend.TryAsk(1, 2)); // character 1 (asker) never enters any zone
        Assert.True(friend.IsNegotiating(2));

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.False(friend.IsNegotiating(2));
    }

    [Fact]
    public void Mentor_MasterOnline_StudentNeverOnline_PendingCleared()
    {
        var (zone, _, _, mentor, _, _) = SetUp(37);
        Enter(zone, 1, "Master");

        Assert.Equal(MentorAskOutcome.Sent, mentor.TryAsk(1, 2, false, false));

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.False(mentor.IsNegotiating(1));
    }

    [Fact]
    public void Party_InviteeOnline_InviterNeverOnline_PendingClearedViaSilentDecline()
    {
        var (zone, _, _, _, party, _) = SetUp(37);
        Enter(zone, 2, "Invitee");

        Assert.Equal(PartyInviteOutcome.Sent, party.TryInvite(1, 10, 0, 2, 10, 0));
        Assert.True(party.IsNegotiating(2));

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.False(party.IsNegotiating(2));
    }

    [Fact]
    public void GuildInvite_AskerOnline_TargetNeverOnline_PendingCleared()
    {
        var (zone, _, _, _, _, guild) = SetUp(37);
        Enter(zone, 1, "Asker");

        Assert.Equal(GuildInviteAskOutcome.Sent, guild.TryAsk(1, 2));
        Assert.True(guild.IsNegotiating(1));

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.False(guild.IsNegotiating(1));
    }
}
