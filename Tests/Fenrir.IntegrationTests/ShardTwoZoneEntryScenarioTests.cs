using Fenrir.IntegrationTests.Fixtures;
using Fenrir.IntegrationTests.Wire;

namespace Fenrir.IntegrationTests;

[Collection("FenrirEnvironment")]
public sealed class ShardTwoZoneEntryScenarioTests
{
    private const string AvatarName = "E2EShard2Bot";

    private const int Tribe = 1;
    private const int PreviousTribe = 1;
    private const int WeaponRawCode = 11;
    private const short ExpectedSpawnMapId = 6;

    private const int StrengthVariableStatSort = 9;
    private const int StrengthPointsToAllocate = 50;

    private const string MousePin = "4343";
    private const int LoginClientVersion = 90354;

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

        var zoneTransfer = await login.ZoneTransferAsync(0, ct);
        Assert.Equal(0, zoneTransfer.Result);
        Assert.Equal(ExpectedSpawnMapId, zoneTransfer.Zone);
        Assert.Equal(_environment.GamePort2, zoneTransfer.Port);
        Assert.NotEqual(_environment.GamePort, zoneTransfer.Port);

        await login.DisposeAsync();

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

        Assert.Equal(1, enterResult.AvatarInfo.Level1);

        await zone.ReadyAsync(0, ct);
        zone.StartBackgroundPump();

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
