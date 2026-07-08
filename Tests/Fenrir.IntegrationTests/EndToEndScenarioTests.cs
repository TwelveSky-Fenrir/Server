using System.Diagnostics;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.IntegrationTests.Fixtures;
using Fenrir.IntegrationTests.Wire;
using Microsoft.Data.SqlClient;

namespace Fenrir.IntegrationTests;

/// <summary>
///     Drives the plan's mandated end-to-end scenario (chapter "Server Logic" verification section) against the
///     ACTUAL Fenrir.LoginServer and Fenrir.GameServer executables over real TCP sockets, backed by a real
///     containerized SQL Server: login -&gt; mouse-PIN create -&gt; character create -&gt; enter world -&gt;
///     validated movement -&gt; kill a monster -&gt; loot -&gt; XP/level-up (server-side only, see remarks) -&gt;
///     buy from an NPC -&gt; chat -&gt; in-process zone transfer -&gt; disconnect -&gt; DB verification.
/// </summary>
/// <remarks>
///     One genuine, verified gap in the current implementation is worked around here, via a direct SQL
///     "Arrange" step rather than skipping the affected leg of the scenario:
///     <list type="bullet">
///         <item>
///             <c>Infrastructure/Fenrir.Data/WriteBehind/DirtyFlags.cs</c> says outright: "Vitals and Progression
///             are raised ... but have no flush reader yet." Only <c>DirtyFlags.Position</c> is ever flushed
///             (<c>PositionWriteBehindHost</c>). XP/Level ARE genuinely computed and applied in-memory by a real
///             kill (<c>MonsterSpawnScheduler.DrainDeaths</c> unconditionally calls
///             <c>Zone.GrantMonsterKillExperience</c>), and there is no wire packet that echoes XP/level back to
///             the client either (no <c>AvatarStatUpdateResponse</c> for kills). So this test drives enough real
///             kills that a level-up must have happened in memory (computed from the same
///             <see cref="ExperienceFormulas" />/<c>LevelProgressionCalculator</c> the server itself runs), but
///             cannot -- and does not -- assert Experience/Level via the database or the wire: there is currently
///             no observable channel for it. Position, inventory and money ARE independently verified via the
///             database, since those really are persisted (position via write-behind, and -- for a
///             disconnecting character specifically -- deterministically via the disconnect path's own
///             synchronous <c>ICharacterWriteBehindFlusher.FlushCharacterNowAsync</c>, awaited before the
///             zone's own Leave command is even posted; inventory/money synchronously inside the handler
///             itself).
///         </item>
///     </list>
///     The prior second gap here -- no wire opcode existed to spend a fresh character's StatPoints into raw
///     Str/Vit/Int/Dex -- is now closed: <c>GenericActionHandler</c> wires up tSort 206
///     (<c>ProcessForStatPlus</c>), so the combat leg now drives a real
///     <see cref="Wire.ZoneBotClient.AllocateStatPointAsync" /> call (category 9, the variable-regime Strength
///     credit -- <c>StatAllocationResolver.BaseStat.Strength</c>) instead of seeding StatStr directly.
///     <see cref="SeedCombatStagingAsync" /> still seeds Money and a spawn position near the planned encounter
///     via direct SQL, but that remains pure harness convenience (skipping a town-to-encounter walk and
///     guaranteeing purchase currency for the NPC-shop leg), not a workaround for any real gap.
/// </remarks>
[Collection("FenrirEnvironment")]
public sealed class EndToEndScenarioTests
{
    private const string AvatarName = "E2EBot01";
    private const string MousePin = "4242";
    private const int LoginClientVersion = 90354; // LoginServerOptions.ExpectedClientVersion default

    // StatAllocationResolver.BaseStat.Strength, variable regime (tStatSort 9-12 = Str/Dex/Vit/Int, cost ==
    // credited amount 1:1). character-creation-level1-redesign (CONFIRMED PRODUCT DECISION): a fresh
    // character now seeds with CreateAvatarService.StartingStatPoint = 50 unspent StatPoints (a Fenrir
    // product default, NOT legacy-cited -- see that constant's own remarks), a ~60x reduction from the old
    // EU33-grant pool of 3175 this constant used to be sized against. Allocating the ENTIRE starting pool
    // into Strength (rather than a fraction "with margin to spare" the way 500-of-3175 was) is a deliberate
    // choice for this combat leg: the character's starter weapon is now unenchanted/uncombined (Enchant 0/
    // Combine 0, see CreateAvatarService.StarterGearEnchant/StarterGearCombine) instead of the old
    // Enchant45/Combine6 elite grant, so maximizing raw Strength is this test's best available lever for
    // clearing StatCalculator.ComputeAttackSuccess's combat-viability floor. This is a genuinely reduced
    // combat-power scenario relative to the old EU33 design; if combat kills start timing out under this
    // budget, the fix is a design change (e.g. an easier planned encounter), not a bigger StatPoints pool --
    // see StartingStatPoint's own remarks for why 50 was chosen.
    private const int StrengthVariableStatSort = 9;
    private const int StrengthPointsToAllocate = 50;

    private readonly FenrirEnvironmentFixture _environment;

    public EndToEndScenarioTests(FenrirEnvironmentFixture environment)
    {
        _environment = environment;
    }

    [Fact]
    public async Task FullScenario_LoginThroughDisconnect_DrivesRealServersAndPersistsExpectedState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var ct = cts.Token;

        var plan = await BuildEncounterPlanAsync(FenrirEnvironmentFixture.PrimaryMapId, ct);

        // ---- Login -> mouse PIN -> character creation ----
        var login = await LoginBotClient.ConnectAsync(_environment.LoginPort, ct);

        LoginResult loginResult;
        try
        {
            loginResult = await login.LoginAsync(FenrirEnvironmentFixture.TestAccountLoginName,
                FenrirEnvironmentFixture.TestAccountPassword, LoginClientVersion, ct);
        }
        catch (Exception ex)
        {
            // Wraps (never swallows) the original failure with the LoginServer's own captured stdout/stderr --
            // a bare "peer closed the connection" IOException on its own gives zero insight into WHY the
            // server ended the session (decode fault, session-state-gate rejection, rate limit, unhandled
            // handler exception, ...); LoginConnectionHost/SessionLoop already log that reason at Warning/Error
            // as it happens, so surfacing it here turns "the socket died" into an actionable root cause on the
            // very first assertion failure instead of requiring a manual re-run with a debugger attached.
            throw new InvalidOperationException(
                $"LoginAsync failed -- see inner exception. Captured LoginServer output:\n{_environment.LoginServerLogSnapshot()}",
                ex);
        }

        Assert.Equal(0, loginResult.Result);
        Assert.Equal(1, loginResult.SecondLoginSort); // RequireSecondPassword=true by default -> PIN mandatory

        var pinResult = await login.CreateMousePinAsync(MousePin, ct);
        Assert.Equal(0, pinResult);

        var createResult = await login.CreateAvatarAsync(0, 0, 0, 0, 0, AvatarName, ct);
        Assert.Equal(0, createResult);

        var characterId = await ReadCharacterIdAsync(AvatarName, ct);
        var seededMoney = Math.Max(200_000, plan.ItemBuyCost * 20);
        await SeedCombatStagingAsync(characterId, plan, seededMoney, ct);

        var zoneTransfer = await login.ZoneTransferAsync(0, ct);
        Assert.Equal(0, zoneTransfer.Result);
        Assert.Equal(FenrirEnvironmentFixture.PrimaryMapId, zoneTransfer.Zone);

        await login.DisposeAsync();

        // ---- Enter world ----
        var zone = await ZoneBotClient.ConnectAsync(_environment.GamePort, ct);

        int handshakeResult;
        try
        {
            handshakeResult = await zone.HandshakeAsync(_environment.TestAccountId, 0, 0, ct);
        }
        catch (Exception ex)
        {
            // Same posture as the LoginAsync call site above: a bare "peer closed the connection"
            // IOException gives zero insight into WHY GameServer ended the session (decode fault,
            // session-state-gate rejection, an unhandled handler exception, ...) -- surface the captured
            // GameServer stdout/stderr so a handshake failure is actionable on the first run instead of
            // requiring a manual re-run with a debugger attached.
            throw new InvalidOperationException(
                $"HandshakeAsync failed -- see inner exception. Captured GameServer output:\n{_environment.GameServerLogSnapshot()}",
                ex);
        }

        Assert.Equal(0, handshakeResult);

        var enterResult = await zone.EnterWorldAsync(_environment.TestAccountId, AvatarName, ct);
        Assert.Equal(AvatarName, enterResult.AvatarInfo.Name);
        // character-creation-level1-redesign (CONFIRMED PRODUCT DECISION): every Fenrir character now starts
        // at a genuine Level 1 (usp_Character_CreateWithStarterKit's own literal, Database/Migrations/
        // 027_character_create_level1_basic_kit.sql), not the old EU33/USE_CUSTOME_CREATE instant-cap grant
        // (Level1 = MAX_LIMIT_LEVEL_NUM = 145) this assertion used to encode. NOTE: Application/
        // Fenrir.Application.Game.Domain/Avatars/AvatarInfoFactory.MaxGeneralExperience still unconditionally
        // reports Exp1 = 2,000,000,000 on this same response, on the now-stale assumption every character is
        // created at the general-level cap -- that Game-side factory is a deferred follow-up gap outside this
        // LoginServer-scoped redesign (see CreateAvatarService's own <remarks>), so Exp1 is deliberately NOT
        // asserted here.
        Assert.Equal(1, enterResult.AvatarInfo.Level1);

        await zone.ReadyAsync(0, ct);
        zone.StartBackgroundPump();

        // ---- Real-wire stat-point allocation (tSort 206, ProcessForStatPlus -- now wired by GenericActionHandler) ----
        // Replaces the former SQL-seeded StatStr workaround (see class remarks): category 9 is the
        // variable-regime Strength credit (StatAllocationResolver.BaseStat.Strength), crediting
        // StrengthPointsToAllocate (the character's ENTIRE starting StatPoints balance -- see that constant's
        // own remarks for why) 1:1 so StatCalculator.ComputeAttackSuccess clears
        // MonsterCombatResolver.ResolvePvmAttack's AttackSuccess floor for real, not via direct SQL.
        // Retried (not a single fire-and-wait) to absorb the same benign staleness window
        // ZoneReadyHandler's own remarks describe: GenericActionHandler silently no-ops if Zone.TryGetPlayer
        // hasn't observed this character yet because the zone tick hasn't drained the posted ZoneCommand.Enter
        // -- a single unacknowledged send is therefore not proof of a wire bug, only of hitting that window
        // once. Same pattern as ShardTwoZoneEntryScenarioTests' own identical retry loop.
        GenericActionResult? statAllocationResult = null;
        var statAllocationDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < statAllocationDeadline && statAllocationResult is null)
        {
            await zone.AllocateStatPointAsync(StrengthVariableStatSort, StrengthPointsToAllocate, ct);
            statAllocationResult = await WaitForGenericActionResultAsync(zone, 206, TimeSpan.FromSeconds(1));
        }

        Assert.True(statAllocationResult is not null,
            "GenericActionResponse for the real-wire stat-point allocation (tSort 206) never arrived. " +
            $"Captured GameServer output:\n{_environment.GameServerLogSnapshot()}");
        Assert.Equal(0, statAllocationResult!.Value.Result);

        var currentX = plan.MonsterX;
        var currentY = plan.MonsterY;
        var currentZ = plan.MonsterZ;

        // ---- Validated movement: close in on an actually-alive monster near the spawned region ----
        var firstMonster = await WaitForMonsterNearAsync(zone, currentX, currentZ, plan.RegionRadius + 100f,
            TimeSpan.FromSeconds(30));
        Assert.True(firstMonster is not null, "No live monster ever appeared near the planned spawn region.");

        var firstLocation = firstMonster!.Value.Data.Action.Location;
        (currentX, currentY, currentZ) = await MoveToAsync(zone, currentX, currentY, currentZ, firstLocation[0],
            firstLocation[1], firstLocation[2], ct);

        // ---- Combat: kill enough monsters that a level-up must have happened in memory (see class remarks) ----
        for (var i = 0; i < plan.KillsNeeded; i++)
        {
            var target = await WaitForMonsterNearAsync(zone, currentX, currentZ, 250f, TimeSpan.FromSeconds(30));
            Assert.True(target is not null,
                $"No live monster available to continue toward the {plan.KillsNeeded}-kill level-up target (kill {i + 1}).");

            await KillMonsterAsync(zone, enterResult.SelfServerIndex, enterResult.SelfUniqueNumber, target.Value,
                currentX, currentY, currentZ, ct);
        }

        // ---- Loot ----
        var loot = await WaitForGroundItemNearAsync(zone, currentX, currentZ,
            GroundItemPickupPolicy.MaxPickupDistance, TimeSpan.FromSeconds(15));
        Assert.True(loot is not null, "No ground item ever appeared after killing monsters.");

        await zone.PickupGroundItemAsync(loot!.Value.ServerIndex, loot.Value.UniqueNumber,
            ContainerMatrix.InventoryPage0, 2, ct);
        var pickupResult = await WaitForGenericActionResultAsync(zone, 201, TimeSpan.FromSeconds(10));
        Assert.True(pickupResult is null || pickupResult.Value.Result == 0,
            "Pickup was explicitly rejected (Result != 0), not merely unconfirmed.");

        // ---- Travel to the NPC and buy from its shop ----
        (currentX, currentY, currentZ) = await MoveToAsync(zone, currentX, currentY, currentZ, plan.NpcX, plan.NpcY,
            plan.NpcZ, ct);

        await zone.BuyFromNpcShopAsync(plan.NpcId, plan.ItemId, 1, ContainerMatrix.InventoryPage0, 3, ct);
        var buyResult = await WaitForGenericActionResultAsync(zone, 215, TimeSpan.FromSeconds(10));
        Assert.True(buyResult is not null, "GenericActionResponse for the NPC purchase (tSort 215) never arrived.");
        Assert.Equal(0, buyResult!.Value.Result);

        // ---- Chat ----
        await zone.LocalChatAsync("hello from the Fenrir integration bot", ct);
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);

        // ---- In-process zone transfer to the second map (ZoneMoveHandler, ADR-0012) ----
        await zone.ZoneMoveAsync(FenrirEnvironmentFixture.SecondaryMapId, FenrirEnvironmentFixture.PrimaryMapId, ct);
        var zoneMoveResult = await WaitForZoneMoveResultAsync(zone, TimeSpan.FromSeconds(10));
        Assert.True(zoneMoveResult is not null, "ZoneMoveResponse never arrived for the in-process map transfer.");
        Assert.Equal(0, zoneMoveResult!.Value);

        // Handoff finishes on the source zone's own next tick (Leave/Enter pair); give it room before disconnecting.
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        // ---- Disconnect ----
        await zone.DisposeAsync();

        // ---- DB verification ----
        await AssertPersistedStateAsync(characterId, plan, seededMoney, ct);
    }

    private async Task<int> ReadCharacterIdAsync(string name, CancellationToken ct)
    {
        await using var connection = await _environment.OpenConnectionAsync();
        await using var command =
            new SqlCommand("SELECT CharacterId FROM game.Characters WHERE Name = @Name;", connection);
        command.Parameters.AddWithValue("@Name", name);
        return (int)(await command.ExecuteScalarAsync(ct))!;
    }

    /// <summary>
    ///     Pure harness convenience, NOT a workaround for a real gap (see class remarks): seeds starting Money
    ///     (so the NPC-purchase leg can always afford <see cref="EncounterPlan.ItemBuyCost" />) and a spawn
    ///     position right next to the planned encounter region (so the scenario doesn't need to walk the
    ///     character across the whole map from its tribe's town first). Both values are hand-picked to make
    ///     that leg reliable, not realistic game balance.
    /// </summary>
    private async Task SeedCombatStagingAsync(int characterId, EncounterPlan plan, long money,
        CancellationToken ct)
    {
        await using var connection = await _environment.OpenConnectionAsync();
        await using var command = new SqlCommand(
            """
            UPDATE game.Characters
            SET Money = @Money, PosX = @PosX, PosY = @PosY, PosZ = @PosZ
            WHERE CharacterId = @CharacterId;
            """, connection);
        command.Parameters.AddWithValue("@Money", money);
        command.Parameters.AddWithValue("@PosX", plan.MonsterX);
        command.Parameters.AddWithValue("@PosY", plan.MonsterY);
        command.Parameters.AddWithValue("@PosZ", plan.MonsterZ);
        command.Parameters.AddWithValue("@CharacterId", characterId);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    ///     Picks the (monster spawn region, town NPC shop) pair in <paramref name="zoneNumber" /> whose monster
    ///     is the lowest <c>RealLevel</c> available (distance used only as a tiebreaker) -- entirely
    ///     data-driven (no hardcoded monster/NPC/item ids), so this adapts to whatever the seed actually
    ///     contains. Also precomputes, from the real <see cref="ExperienceFormulas" />, how many kills of that
    ///     specific monster a level-1 character needs to cross level 1's XP band.
    /// </summary>
    /// <remarks>
    ///     character-creation-level1-redesign (CONFIRMED PRODUCT DECISION) fallout: this used to order purely
    ///     by (monster spawn region, NPC shop) proximity, which was fine when a freshly created character was
    ///     the old EU33 max-level/elite-gear grant and could one-shot anything on this map regardless of which
    ///     pair happened to be geometrically closest. Now that a fresh character is genuinely Level 1 with only
    ///     <see cref="StrengthPointsToAllocate" /> unspent Strength and an unenchanted starter weapon (see this
    ///     class's own remarks on that constant), the nearest-pair choice could just as easily land on a
    ///     RealLevel-16+ monster (world.Monsters seed data on this map ranges from RealLevel 1/Life 35 up past
    ///     RealLevel 16/Life 1130+) as the RealLevel-1 one, and <see cref="KillMonsterAsync" />'s 30-second
    ///     combat budget only tolerates the weakest monster actually available. RealLevel first, distance
    ///     second, keeps this a genuine data-driven pick (whatever the seed's lowest-level monster on this map
    ///     turns out to be) rather than hardcoding a specific MonsterId.
    /// </remarks>
    private async Task<EncounterPlan> BuildEncounterPlanAsync(short zoneNumber, CancellationToken ct)
    {
        await using var connection = await _environment.OpenConnectionAsync();

        int monsterId;
        float monsterX, monsterY, monsterZ;
        int regionRadius;
        int npcId;
        float npcX, npcY, npcZ;
        int itemId, buyCost;
        string itemName;

        await using (var command = new SqlCommand(
                         """
                         SELECT TOP 1
                             msr.LocationX, msr.LocationY, msr.LocationZ, msr.Radius, msr.MonsterId,
                             zns.NpcId, zns.PosX, zns.PosY, zns.PosZ,
                             nsi.ItemId, i.BuyCost, i.Name
                         FROM world.MonsterSpawnRegions msr
                         JOIN world.Monsters m ON m.MonsterId = msr.MonsterId
                         CROSS JOIN world.ZoneNpcSpawns zns
                         JOIN world.NpcMenuOptions mo ON mo.NpcId = zns.NpcId AND mo.SlotIndex = 4 AND mo.OptionId = 2
                         JOIN world.NpcShopItems nsi ON nsi.NpcId = zns.NpcId
                         JOIN world.Items i ON i.ItemId = nsi.ItemId
                         WHERE msr.ZoneNumber = @Zone AND zns.ZoneNumber = @Zone
                           AND msr.MonsterId IS NOT NULL AND nsi.ItemId IS NOT NULL
                           AND i.CheckNpcShop = 2 AND i.BuyCost BETWEEN 1 AND 200000
                         ORDER BY m.RealLevel ASC,
                             (SQUARE(msr.LocationX - zns.PosX) + SQUARE(msr.LocationZ - zns.PosZ)) ASC;
                         """, connection))
        {
            command.Parameters.AddWithValue("@Zone", zoneNumber);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException(
                    $"No (monster spawn region, town NPC shop) pair found for zone {zoneNumber} in the seeded " +
                    "reference data -- cannot plan the combat/loot/purchase leg of the scenario.");

            monsterX = reader.GetInt32(0);
            monsterY = reader.GetInt32(1);
            monsterZ = reader.GetInt32(2);
            regionRadius = reader.GetInt32(3);
            monsterId = reader.GetInt32(4);
            npcId = reader.GetInt32(5);
            // world.ZoneNpcSpawns.PosX/Y/Z are REAL, unlike world.MonsterSpawnRegions.LocationX/Y/Z (INT).
            npcX = reader.GetFloat(6);
            npcY = reader.GetFloat(7);
            npcZ = reader.GetFloat(8);
            itemId = reader.GetInt32(9);
            buyCost = reader.GetInt32(10);
            itemName = reader.GetString(11);
        }

        int monsterRealLevel, monsterGeneralExperience;
        await using (var command = new SqlCommand(
                         "SELECT RealLevel, GeneralExperience FROM world.Monsters WHERE MonsterId = @MonsterId;",
                         connection))
        {
            command.Parameters.AddWithValue("@MonsterId", monsterId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException($"MonsterId {monsterId} not found in world.Monsters.");
            monsterRealLevel = reader.GetInt16(0);
            monsterGeneralExperience = reader.GetInt32(1);
        }

        var fixedLevel = ExperienceFormulas.ReturnFixedLevel(1);
        var rawGain = ExperienceFormulas.ComputeMonsterKillExperience(fixedLevel, monsterRealLevel,
            monsterGeneralExperience);
        var perKillGain = ExperienceFormulas.ApplyRebirthDivisor(rawGain, 1);

        // 230 safely clears level 1's ExpRangeMax=222 (world.Levels seed); capped so a pathologically low
        // per-kill yield can't turn this into an unbounded grind -- see the class remarks about what this test
        // can and can't observe once the level-up is reached anyway.
        var killsNeeded = perKillGain <= 0 ? 1 : Math.Clamp((int)Math.Ceiling(230.0 / perKillGain) + 1, 1, 40);

        return new EncounterPlan(monsterX, monsterY, monsterZ, regionRadius, monsterId, killsNeeded, npcId, npcX,
            npcY, npcZ, itemId, buyCost, itemName);
    }

    private async Task AssertPersistedStateAsync(int characterId, EncounterPlan plan, long seededMoney,
        CancellationToken ct)
    {
        short mapId = -1;
        long money = -1;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            await using var connection = await _environment.OpenConnectionAsync();
            await using var command =
                new SqlCommand("SELECT MapId, Money FROM game.Characters WHERE CharacterId = @Id;", connection);
            command.Parameters.AddWithValue("@Id", characterId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                mapId = reader.GetInt16(0);
                money = reader.GetInt64(1);
            }

            if (mapId == FenrirEnvironmentFixture.SecondaryMapId)
                break;

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }

        // MapId (part of the position row): persisted deterministically by the disconnect path's synchronous
        // ICharacterWriteBehindFlusher.FlushCharacterNowAsync, awaited BEFORE the zone's own Leave command is
        // even posted (see GameConnectionHost's disconnect-cleanup remarks) -- not a race against the zone
        // tick's own registry removal, and no longer dependent on the best-effort RequestImmediateFlush nudge.
        Assert.Equal(FenrirEnvironmentFixture.SecondaryMapId, mapId);

        // Money: debited synchronously inside BuyShopItemHandler's own SQL call, not write-behind -- always current.
        Assert.True(money < seededMoney,
            $"Expected Money ({money}) to be less than the seeded {seededMoney} after the NPC purchase.");

        await using (var itemConnection = await _environment.OpenConnectionAsync())
        await using (var command = new SqlCommand(
                         "SELECT COUNT(*) FROM game.CharacterItems WHERE CharacterId = @Id AND ItemId = @ItemId;",
                         itemConnection))
        {
            command.Parameters.AddWithValue("@Id", characterId);
            command.Parameters.AddWithValue("@ItemId", plan.ItemId);
            var itemCount = (int)(await command.ExecuteScalarAsync(ct))!;
            Assert.True(itemCount > 0,
                $"Expected at least one game.CharacterItems row for the purchased ItemId {plan.ItemId} ({plan.ItemName}).");
        }
    }

    private static async Task<MonsterSnapshot?> WaitForMonsterNearAsync(ZoneBotClient zone, float x, float z,
        float radius, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var candidate in zone.Monsters.Values)
            {
                if (candidate.Data.LifeValue <= 0)
                    continue;

                var location = candidate.Data.Action.Location;
                var dx = location[0] - x;
                var dz = location[2] - z;
                if (dx * dx + dz * dz <= radius * radius)
                    return candidate;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        return null;
    }

    private static async Task<GroundItemSnapshot?> WaitForGroundItemNearAsync(ZoneBotClient zone, float x, float z,
        float radius, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var candidate in zone.GroundItems.Values)
            {
                var location = candidate.Data.Location;
                var dx = location[0] - x;
                var dz = location[2] - z;
                if (dx * dx + dz * dz <= radius * radius)
                    return candidate;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        return null;
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

    private static async Task<int?> WaitForZoneMoveResultAsync(ZoneBotClient zone, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (zone.TryTakeZoneMoveResult(out var result))
                return result;

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return null;
    }

    private static async Task KillMonsterAsync(ZoneBotClient zone, int selfServerIndex, uint selfUniqueNumber,
        MonsterSnapshot target, float x, float y, float z, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (!zone.Monsters.TryGetValue(target.ServerIndex, out var current) ||
                current.UniqueNumber != target.UniqueNumber || current.Data.LifeValue <= 0)
                return;

            await zone.AttackMonsterAsync(selfServerIndex, selfUniqueNumber, target.ServerIndex,
                target.UniqueNumber, x, y, z, ct);
            await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
        }

        throw new TimeoutException(
            $"Monster {target.ServerIndex}/{target.UniqueNumber} never died within the combat time budget.");
    }

    /// <summary>
    ///     Repeated small, MovementRules.IsPlausible-respecting steps rather than one big teleport -- every step
    ///     is a genuine, independently validated CZ_AVATAR_ACTION_SEND, not a single lucky first-move exemption.
    ///     Returns the resulting position (async methods can't take ref/out parameters).
    /// </summary>
    private static async Task<(float X, float Y, float Z)> MoveToAsync(ZoneBotClient zone, float x, float y,
        float z, float targetX, float targetY, float targetZ, CancellationToken ct)
    {
        const float maxPlausibleSpeedPerSecond = 20f; // GameServerOptions.MaxPlausibleSpeedPerSecond default
        const float slackUnits = 1f; // MovementRules.SlackUnits
        const float safetyFactor = 0.7f;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var dx = targetX - x;
            var dz = targetZ - z;
            var remaining = MathF.Sqrt(dx * dx + dz * dz);
            if (remaining < 2f)
            {
                x = targetX;
                y = targetY;
                z = targetZ;
                await zone.MoveAsync(x, y, z, 0f, ct);
                return (x, y, z);
            }

            var elapsedSeconds = (float)stopwatch.Elapsed.TotalSeconds;
            stopwatch.Restart();
            var budget = (maxPlausibleSpeedPerSecond * MathF.Max(elapsedSeconds, 0.05f) + slackUnits) *
                         safetyFactor;
            var step = MathF.Min(remaining, budget);

            x += dx / remaining * step;
            z += dz / remaining * step;
            y = targetY; // MovementRules only speed-checks X/Z; Y (height) isn't part of the plausibility gate

            await zone.MoveAsync(x, y, z, 0f, ct);
            await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        }
    }

    private readonly record struct EncounterPlan(
        float MonsterX,
        float MonsterY,
        float MonsterZ,
        int RegionRadius,
        int MonsterId,
        int KillsNeeded,
        int NpcId,
        float NpcX,
        float NpcY,
        float NpcZ,
        int ItemId,
        int ItemBuyCost,
        string ItemName);
}
