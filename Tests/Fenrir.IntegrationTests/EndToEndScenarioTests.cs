using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.IntegrationTests.Fixtures;
using Fenrir.IntegrationTests.Wire;
using Microsoft.Data.SqlClient;

namespace Fenrir.IntegrationTests;

[Collection("FenrirEnvironment")]
public sealed class EndToEndScenarioTests
{
    private const string AvatarName = "E2EBot01";
    private const string MousePin = "4242";
    private const int LoginClientVersion = 90354;

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

        var login = await LoginBotClient.ConnectAsync(_environment.LoginPort, ct);

        LoginResult loginResult;
        try
        {
            loginResult = await login.LoginAsync(FenrirEnvironmentFixture.TestAccountLoginName,
                FenrirEnvironmentFixture.TestAccountPassword, LoginClientVersion, ct);
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

        var createResult = await login.CreateAvatarAsync(0, 0, 0, 0, 0, AvatarName, ct);
        Assert.Equal(0, createResult);

        var characterId = await ReadCharacterIdAsync(AvatarName, ct);
        var seededMoney = Math.Max(200_000, plan.ItemBuyCost * 20);
        await SeedCombatStagingAsync(characterId, plan, seededMoney, ct);

        var zoneTransfer = await login.ZoneTransferAsync(0, ct);
        Assert.Equal(0, zoneTransfer.Result);
        Assert.Equal(FenrirEnvironmentFixture.PrimaryMapId, zoneTransfer.Zone);

        await login.DisposeAsync();

        var zone = await ZoneBotClient.ConnectAsync(_environment.GamePort, ct);

        int handshakeResult;
        try
        {
            handshakeResult = await zone.HandshakeAsync(_environment.TestAccountId, 0, 0, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"HandshakeAsync failed -- see inner exception. Captured GameServer output:\n{_environment.GameServerLogSnapshot()}",
                ex);
        }

        Assert.Equal(0, handshakeResult);

        var enterResult = await zone.EnterWorldAsync(_environment.TestAccountId, AvatarName, ct,
            plan.MonsterX, plan.MonsterY, plan.MonsterZ);
        Assert.Equal(AvatarName, enterResult.AvatarInfo.Name);
        Assert.Equal(1, enterResult.AvatarInfo.Level1);

        await zone.ReadyAsync(0, ct);
        zone.StartBackgroundPump();

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

        var firstMonster = await WaitForMonsterNearAsync(zone, currentX, currentZ, plan.RegionRadius + 100f,
            TimeSpan.FromSeconds(30));
        Assert.True(firstMonster is not null, "No live monster ever appeared near the planned spawn region.");

        var firstLocation = firstMonster!.Value.Data.Action.Location;
        (currentX, currentY, currentZ) = await MoveToAsync(zone, currentX, currentY, currentZ, firstLocation[0],
            firstLocation[1], firstLocation[2], ct);

        for (var i = 0; i < plan.KillsNeeded; i++)
        {
            var target = await WaitForMonsterNearAsync(zone, currentX, currentZ, 250f, TimeSpan.FromSeconds(30));
            Assert.True(target is not null,
                $"No live monster available to continue toward the {plan.KillsNeeded}-kill level-up target (kill {i + 1}).");

            await KillMonsterAsync(zone, enterResult.SelfServerIndex, enterResult.SelfUniqueNumber, target.Value,
                currentX, currentY, currentZ, ct);
        }

        var loot = await WaitForGroundItemNearAsync(zone, currentX, currentZ,
            GroundItemPickupPolicy.MaxPickupDistance, TimeSpan.FromSeconds(15));
        Assert.True(loot is not null, "No ground item ever appeared after killing monsters.");

        await zone.PickupGroundItemAsync(loot!.Value.ServerIndex, loot.Value.UniqueNumber,
            ContainerMatrix.InventoryPage0, 2, ct);
        var pickupResult = await WaitForGenericActionResultAsync(zone, 201, TimeSpan.FromSeconds(10));
        Assert.True(pickupResult is null || pickupResult.Value.Result == 0,
            "Pickup was explicitly rejected (Result != 0), not merely unconfirmed.");

        (currentX, currentY, currentZ) = await MoveToAsync(zone, currentX, currentY, currentZ, plan.NpcX, plan.NpcY,
            plan.NpcZ, ct);

        await zone.BuyFromNpcShopAsync(plan.NpcId, plan.ItemId, 1, ContainerMatrix.InventoryPage0, 3, ct);
        var buyResult = await WaitForGenericActionResultAsync(zone, 215, TimeSpan.FromSeconds(10));
        Assert.True(buyResult is not null, "GenericActionResponse for the NPC purchase (tSort 215) never arrived.");
        Assert.Equal(0, buyResult!.Value.Result);

        await zone.LocalChatAsync("hello from the Fenrir integration bot", ct);
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);

        await zone.ZoneMoveAsync(FenrirEnvironmentFixture.SecondaryMapId, FenrirEnvironmentFixture.PrimaryMapId, ct);
        var zoneMoveResult = await WaitForZoneMoveResultAsync(zone, TimeSpan.FromSeconds(10));
        Assert.True(zoneMoveResult is not null, "ZoneMoveResponse never arrived for the in-process map transfer.");
        Assert.Equal(0, zoneMoveResult!.Value);

        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        await zone.DisposeAsync();

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

        Assert.Equal(FenrirEnvironmentFixture.SecondaryMapId, mapId);

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

    private static async Task<(float X, float Y, float Z)> MoveToAsync(ZoneBotClient zone, float x, float y,
        float z, float targetX, float targetY, float targetZ, CancellationToken ct)
    {
        const float maxPlausibleMoveDistance = 666f;
        const float stepDistance = maxPlausibleMoveDistance * 0.6f;

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

            var step = MathF.Min(remaining, stepDistance);
            var fraction = step / remaining;
            x += dx * fraction;
            z += dz * fraction;
            y += (targetY - y) * fraction;

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
