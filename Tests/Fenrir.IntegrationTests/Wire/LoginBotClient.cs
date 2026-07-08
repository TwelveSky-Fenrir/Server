using Fenrir.Network.Compression;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.IntegrationTests.Wire;

/// <summary>
///     Drives Fenrir.LoginServer's wire protocol over a real socket, playing the client's half of every
///     exchange the plan's scenario needs: greet -&gt; login -&gt; mouse-PIN create -&gt; character create -&gt;
///     zone transfer. Every packet is hand-built/hand-parsed (see WireScalars's doc comment for why), never a
///     Write-then-TryRead round trip against itself.
/// </summary>
public sealed class LoginBotClient : IAsyncDisposable
{
    private const int LoginTrainAvatarSlotCount = 3;
    private readonly RawWireConnection _connection;

    private LoginBotClient(RawWireConnection connection)
    {
        _connection = connection;
    }

    public ValueTask DisposeAsync()
    {
        return _connection.DisposeAsync();
    }

    public static async Task<LoginBotClient> ConnectAsync(int port, CancellationToken ct)
    {
        var connection = await RawWireConnection.ConnectAsync(port, ct);
        var bot = new LoginBotClient(connection);
        await bot.ReadGreetingAsync(ct);
        return bot;
    }

    /// <summary>op0 LC_CONNECT_OK_RECV -- whole-frame XOR'd (XorPacketGlobal), fixed size, always the first server byte.</summary>
    private async Task ReadGreetingAsync(CancellationToken ct)
    {
        var frame = await _connection.ReadExactAsync(1 + LoginGreetingResponse.PayloadSize, ct);
        WireXor.ApplyPacketXor(frame);

        if (frame[0] != LoginGreetingResponse.Opcode)
            throw new InvalidOperationException(
                $"Expected LoginGreetingResponse (op {LoginGreetingResponse.Opcode}), got op {frame[0]}.");

        // Reserved(20) then RandomNumber: payload offset 20, frame offset 1+20.
        var randomNumber = WireScalars.ReadInt32(frame.AsSpan(21, 4));
        _connection.SeedOutboundStreamKey(randomNumber);
    }

    /// <summary>op11 CL_LOGIN_SEND, then drains the full 6-packet SEND_LOGIN train (LoginTrain.Send).</summary>
    public async Task<LoginResult> LoginAsync(string id, string password, int version, CancellationToken ct)
    {
        var payload = new byte[LoginRequest.PayloadSize];
        WireScalars.WriteFixedString(payload.AsSpan(0, 255), id);
        WireScalars.WriteFixedString(payload.AsSpan(255, 33), password);
        WireScalars.WriteInt32(payload.AsSpan(288, 4), version);
        new LoginAdapterInfo
        {
            AdapterName = "",
            PhysicalAddressLength = 0,
            PhysicalAddress = new byte[8],
            IPAddress = ""
        }.Write(payload.AsSpan(292, LoginAdapterInfo.WireSize));

        await SendAsync(LoginRequest.Opcode, payload, ct);

        var loginRecvFrame = await _connection.ReadExactAsync(1 + LoginResponse.PayloadSize, ct);
        WireXor.ApplyPacketXor(loginRecvFrame);
        if (loginRecvFrame[0] != LoginResponse.Opcode)
            throw new InvalidOperationException(
                $"Expected LoginResponse (op {LoginResponse.Opcode}), got op {loginRecvFrame[0]}.");
        var result = WireScalars.ReadInt32(loginRecvFrame.AsSpan(1, 4));
        // Payload layout: Result(4) Id(255) UserSort(4) GoodFellow(4) LoginPlace(4) LoginPremium(4) SecondLoginSort(4) ...
        var secondLoginSort = WireScalars.ReadInt32(loginRecvFrame.AsSpan(1 + 4 + 255 + 4 + 4 + 4 + 4, 4));

        // 3x AvatarRosterResponse: XorFieldAvatar only touches specific fields, never the opcode byte itself
        // (see FrameWriter.WriteFrame); irrelevant here since the content isn't needed for a fresh account.
        for (var i = 0; i < LoginTrainAvatarSlotCount; i++)
            await _connection.ReadExactAsync(1 + AvatarRosterResponse.PayloadSize, ct);

        await _connection.ReadExactAsync(1 + WorldRecommendationResponse.PayloadSize, ct);
        await _connection.ReadExactAsync(1 + WorldRecommendationFinalResponse.PayloadSize, ct);

        return new LoginResult(result, secondLoginSort);
    }

    /// <summary>op13 CL_CREATE_MOUSE_PASSWORD_SEND -- only legal from PinRequired (fresh account, no stored PIN yet).</summary>
    public async Task<int> CreateMousePinAsync(string pin, CancellationToken ct)
    {
        var payload = new byte[CreateMousePinRequest.PayloadSize];
        WireScalars.WriteFixedString(payload.AsSpan(0, 5), pin);
        await SendAsync(CreateMousePinRequest.Opcode, payload, ct);

        var frame = await _connection.ReadExactAsync(1 + CreateMousePinResponse.PayloadSize, ct);
        if (frame[0] != CreateMousePinResponse.Opcode)
            throw new InvalidOperationException(
                $"Expected CreateMousePinResponse (op {CreateMousePinResponse.Opcode}), got op {frame[0]}.");
        return WireScalars.ReadInt32(frame.AsSpan(1, 4));
    }

    /// <summary>
    ///     op17 CL_CREATE_AVATAR_SEND2. <paramref name="weapon" /> must be one of the CALLING
    ///     <paramref name="previousTribe" />'s own three legal <c>RawWeaponCode</c> values (previousTribe 0:
    ///     5/6/7, previousTribe 1: 11/12/13, previousTribe 2: 17/18/19 --
    ///     Server/ts25login/S04_MyWork02.cpp:756-778/784-806/812-834, corroborated by
    ///     <c>world.StarterKitEquipment</c>'s own seed, keyed by <c>PreviousTribe</c> not <c>Tribe</c> --
    ///     see <c>Database/Migrations/Seed/world/082_starter_kit_equipment.sql</c>) or
    ///     <c>CreateAvatarService.TryResolveWeaponItemId</c> can't resolve a starter-kit weapon item and the
    ///     whole request is rejected as <c>CreateAvatarOutcome.InvalidWeapon</c> (a hard disconnect, no reply
    ///     frame at all -- NOT the "server ignores this field" behavior an earlier version of this method
    ///     assumed). <paramref name="previousTribe" /> defaults to 0 (matching every pre-existing caller's own
    ///     tribe-0/previousTribe-0 avatar, where the two happen to coincide) -- pass it explicitly whenever
    ///     <paramref name="tribe" /> isn't 0, since <c>EnterWorldService.IsTribeAndPreviousTribeConsistent</c>
    ///     requires a main-faction Tribe (0-2) to carry a PreviousTribe exactly equal to itself (the fourth
    ///     faction, Tribe 3, is the one legitimate case where they may differ) or the character can never
    ///     enter the world at all. Weapon defaults to 5 (previousTribe 0's first legal code).
    /// </summary>
    public async Task<int> CreateAvatarAsync(int avatarPost, int tribe, int gender, int head, int face,
        string avatarName, CancellationToken ct, int weapon = 5, int previousTribe = 0)
    {
        var payload = new byte[CreateAvatarRequest.PayloadSize];
        WireScalars.WriteInt32(payload.AsSpan(0, 4), avatarPost);
        WireScalars.WriteInt32(payload.AsSpan(4, 4), tribe);
        WireScalars.WriteInt32(payload.AsSpan(8, 4), previousTribe);
        WireScalars.WriteInt32(payload.AsSpan(12, 4), gender);
        WireScalars.WriteInt32(payload.AsSpan(16, 4), head);
        WireScalars.WriteInt32(payload.AsSpan(20, 4), face);
        WireScalars.WriteInt32(payload.AsSpan(24, 4), weapon);
        WireScalars.WriteFixedString(payload.AsSpan(28, 13), avatarName);
        await SendAsync(CreateAvatarRequest.Opcode, payload, ct);

        var frame = await _connection.ReadExactAsync(1 + CreateAvatarResponse.PayloadSize, ct);
        if (frame[0] != CreateAvatarResponse.Opcode)
            throw new InvalidOperationException(
                $"Expected CreateAvatarResponse (op {CreateAvatarResponse.Opcode}), got op {frame[0]}.");
        return WireScalars.ReadInt32(frame.AsSpan(1, 4));
    }

    /// <summary>op22 CL_DEMAND_ZONE_SERVER_INFO_SEND -- login-to-zone handover (mints the single-use ticket).</summary>
    public async Task<ZoneTransferResult> ZoneTransferAsync(int avatarPost, CancellationToken ct)
    {
        var payload = new byte[ZoneTransferRequest.PayloadSize];
        WireScalars.WriteInt32(payload.AsSpan(0, 4), avatarPost);
        await SendAsync(ZoneTransferRequest.Opcode, payload, ct);

        var frame = await _connection.ReadExactAsync(1 + ZoneTransferResponse.PayloadSize, ct);
        if (frame[0] != ZoneTransferResponse.Opcode)
            throw new InvalidOperationException(
                $"Expected ZoneTransferResponse (op {ZoneTransferResponse.Opcode}), got op {frame[0]}.");

        var result = WireScalars.ReadInt32(frame.AsSpan(1, 4));
        var ip = WireScalars.ReadFixedString(frame.AsSpan(5, 16));
        var port = WireScalars.ReadInt32(frame.AsSpan(21, 4));
        var zone = WireScalars.ReadInt32(frame.AsSpan(25, 4));
        return new ZoneTransferResult(result, ip, port, zone);
    }

    /// <summary>
    ///     Every client-&gt;server frame is <c>CLIENT_PACKET</c>-framed on the wire (<c>WireHeaderSizes.ClientPacketSize</c>
    ///     = 9: <c>int tPacket1 + int tPacket2 + BYTE tProtocol</c>, opcode at header offset 8), not the bare
    ///     opcode-then-payload shape a server response uses -- <see cref="Fenrir.Network.Framing.FrameDecoder" />
    ///     unconditionally requires and slices off those leading 9 bytes before locating the opcode/payload for
    ///     any INCOMING frame, on both LoginServer and GameServer. tPacket1/tPacket2 carry no framing
    ///     information the server ever validates (see FrameDecoder's own remarks), so any filler value works --
    ///     left zero here.
    /// </summary>
    private async Task SendAsync(byte opcode, byte[] payload, CancellationToken ct)
    {
        var frame = new byte[WireHeaderSizes.ClientPacketSize + payload.Length];
        frame[8] = opcode;
        payload.CopyTo(frame.AsSpan(WireHeaderSizes.ClientPacketSize));
        await _connection.SendAsync(frame, ct);
    }
}

public readonly record struct LoginResult(int Result, int SecondLoginSort);

public readonly record struct ZoneTransferResult(int Result, string Ip, int Port, int Zone);
