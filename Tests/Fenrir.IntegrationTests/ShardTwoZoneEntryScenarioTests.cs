using Fenrir.IntegrationTests.Fixtures;
using Fenrir.IntegrationTests.Wire;

namespace Fenrir.IntegrationTests;

/// <summary>
///     Drives Login -&gt; GameServer -&gt; InWorld against shard 2 specifically (
///     <see cref="FenrirEnvironmentFixture.ShardId2" />,
///     hosting maps 6/11/140 per the real seed <c>Database/Migrations/Seed/admin/005_shard_map_assignments.sql</c>),
///     unlike <see cref="EndToEndScenarioTests" /> which only ever exercises shard 1 (map 1). This closes a gap a
///     prior audit round left open: every existing wire-level integration coverage only ever connected to a
///     single-shard topology, so a Login-side bug in resolving <c>runtime.GameServerDirectory</c> +
///     <c>admin.ShardMapAssignments</c> to a NON-default shard's own Host:Port, or a GameServer-side bug specific
///     to booting as shard 2, would have gone completely undetected.
/// </summary>
/// <remarks>
///     Minimal by design: unlike <see cref="EndToEndScenarioTests" />'s full combat/loot/purchase/zone-transfer
///     tour, this only needs to prove the shard-2 connection path itself works end to end -- handshake, enter
///     world, ready, and one real round-trip request/response pair. <c>tSort 206</c>
///     (<c>ProcessForStatPlus</c>, <see cref="ZoneBotClient.AllocateStatPointAsync" />) is chosen as that
///     round trip because it depends only on character/zone state already guaranteed by a fresh avatar (starting
///     StatPoints balance), not on monster spawns or NPC shop data existing for these specific maps the way
///     <see cref="EndToEndScenarioTests" />'s combat leg does for map 1.
/// </remarks>
[Collection("FenrirEnvironment")]
public sealed class ShardTwoZoneEntryScenarioTests
{
    private const string AvatarName = "E2EShard2Bot";

    // CreateAvatarService.SpawnMapIdByTribe = [1, 6, 11, 140] -- tribe 1 spawns on map 6, which
    // 005_shard_map_assignments.sql assigns to shard 2. EnterWorldService.IsTribeAndPreviousTribeConsistent
    // requires a main-faction Tribe (0-2) to carry a PreviousTribe exactly equal to itself, so PreviousTribe
    // must be sent as 1 too (see LoginBotClient.CreateAvatarAsync's own remarks) -- which in turn means the
    // weapon code must be one of PreviousTribe 1's own three legal RawWeaponCode values (11/12/13), not
    // PreviousTribe 0's (5/6/7) the way EndToEndScenarioTests' tribe-0 avatar uses.
    private const int Tribe = 1;
    private const int PreviousTribe = 1;
    private const int WeaponRawCode = 11;
    private const short ExpectedSpawnMapId = 6;

    // character-creation-level1-redesign (CONFIRMED PRODUCT DECISION): a fresh character now seeds with
    // CreateAvatarService.StartingStatPoint = 50 unspent StatPoints (a Fenrir product default, NOT
    // legacy-cited -- see that constant's own remarks), down from the old EU33-grant pool of 3175 this
    // constant used to be sized against with headroom to spare. This scenario only needs a real round trip
    // to succeed (see class remarks -- no combat leg here), so allocating the full starting pool keeps the
    // request comfortably affordable without needing to track the exact new pool size at two call sites.
    private const int StrengthVariableStatSort = 9; // StatAllocationResolver.BaseStat.Strength
    private const int StrengthPointsToAllocate = 50;

    private const string MousePin = "4343";
    private const int LoginClientVersion = 90354; // LoginServerOptions.ExpectedClientVersion default

    private readonly FenrirEnvironmentFixture _environment;

    public ShardTwoZoneEntryScenarioTests(FenrirEnvironmentFixture environment)
    {
        _environment = environment;
    }

    [Fact]
    public async Task ZoneEntry_AgainstShardTwo_ReachesInWorldAndRoundTripsARealPacket()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = cts.Token;

        // ---- Login -> mouse PIN -> character creation (dedicated second account, see fixture remarks) ----
        var login = await LoginBotClient.ConnectAsync(_environment.LoginPort, ct);

        LoginResult loginResult;
        try
        {
            loginResult = await login.LoginAsync(FenrirEnvironmentFixture.TestAccountLoginName2,
                FenrirEnvironmentFixture.TestAccountPassword2, LoginClientVersion, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"LoginAsync failed -- see inner exception. Captured LoginServer output:\n{_environment.LoginServerLogSnapshot()}",
                ex);
        }

        Assert.Equal(0, loginResult.Result);
        Assert.Equal(1, loginResult.SecondLoginSort);

        var pinResult = await login.CreateMousePinAsync(MousePin, ct);
        Assert.Equal(0, pinResult);

        var createResult = await login.CreateAvatarAsync(0, Tribe, 0, 0, 0, AvatarName, ct, WeaponRawCode,
            PreviousTribe);
        Assert.True(createResult == 0,
            $"createResult was {createResult}, not 0. Captured LoginServer output:\n{_environment.LoginServerLogSnapshot()}");

        // ---- Zone transfer: must resolve to shard 2's own live Host:Port, not shard 1's ----
        var zoneTransfer = await login.ZoneTransferAsync(0, ct);
        Assert.Equal(0, zoneTransfer.Result);
        Assert.Equal(ExpectedSpawnMapId, zoneTransfer.Zone);
        Assert.Equal(_environment.GamePort2, zoneTransfer.Port);
        Assert.NotEqual(_environment.GamePort, zoneTransfer.Port);

        await login.DisposeAsync();

        // ---- Enter world against shard 2, using the Host:Port the ticket/transfer response actually returned ----
        var zone = await ZoneBotClient.ConnectAsync(zoneTransfer.Port, ct);

        int handshakeResult;
        try
        {
            handshakeResult = await zone.HandshakeAsync(_environment.TestAccountId2, 0, 0, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"HandshakeAsync failed -- see inner exception. Captured shard-2 GameServer output:\n{_environment.GameServer2LogSnapshot()}",
                ex);
        }

        Assert.Equal(0, handshakeResult);

        EnterWorldResult enterResult;
        try
        {
            enterResult = await zone.EnterWorldAsync(_environment.TestAccountId2, AvatarName, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"EnterWorldAsync failed -- see inner exception. Captured shard-2 GameServer output:\n{_environment.GameServer2LogSnapshot()}",
                ex);
        }

        Assert.Equal(AvatarName, enterResult.AvatarInfo.Name);

        // character-creation-level1-redesign (CONFIRMED PRODUCT DECISION): every freshly created character
        // (any tribe, any shard) now genuinely persists at Level 1 (Database/Migrations/
        // 027_character_create_level1_basic_kit.sql), closing the discrepancy this test used to document --
        // this assertion previously could not be added here because the pre-redesign stored procedure
        // hardcoded Level = 145 with no corresponding C# constant to even name. Now asserted the same way
        // EndToEndScenarioTests asserts it for shard 1.
        Assert.Equal(1, enterResult.AvatarInfo.Level1);

        // ---- Ready: op13 CL_MOVE_ZONE_OK_SEND, no reply, session transitions to InWorld server-side ----
        await zone.ReadyAsync(0, ct);
        zone.StartBackgroundPump();

        // ---- Real round-trip packet: tSort 206 ProcessForStatPlus, request -> GenericActionResponse ----
        // Retried (not a single fire-and-wait) to absorb the same benign staleness window
        // ZoneReadyHandler's own remarks describe: GenericActionHandler silently no-ops if
        // Zone.TryGetPlayer hasn't observed this character yet because the zone tick hasn't drained the
        // posted ZoneCommand.Enter -- a single unacknowledged send is therefore not proof of a wire bug,
        // only of hitting that window once.
        GenericActionResult? statAllocationResult = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline && statAllocationResult is null)
        {
            await zone.AllocateStatPointAsync(StrengthVariableStatSort, StrengthPointsToAllocate, ct);
            statAllocationResult = await WaitForGenericActionResultAsync(zone, 206, TimeSpan.FromSeconds(1));
        }

        Assert.True(statAllocationResult is not null,
            "GenericActionResponse for the real-wire stat-point allocation (tSort 206) never arrived from shard 2 " +
            $"-- captured shard-2 GameServer output:\n{_environment.GameServer2LogSnapshot()}");
        Assert.Equal(0, statAllocationResult!.Value.Result);

        await zone.DisposeAsync();
    }

    private static async Task<GenericActionResult?> WaitForGenericActionResultAsync(ZoneBotClient zone, int sort,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (zone.TryTakeGenericActionResult(out var result) && result.Sort == sort)
                return result;

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return null;
    }
}
