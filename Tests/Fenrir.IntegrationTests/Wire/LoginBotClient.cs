using Fenrir.Network.Compression;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.IntegrationTests.Wire;

/// <summary>
///     Drives Fenrir.LoginServer's wire protocol over a real socket, playing the client's half of every
///     exchange the plan's scenario needs: greet -&gt; login -&gt; mouse-PIN create -&gt; character create -&gt;
///     zone transfer. Every packet is hand-built/hand-parsed (see WireScalars's doc comment for why), never a
///     Write-then-TryRead round trip against itself.
/// </summary>
public sealed class LoginBotClient : IAsyncDisposable
{
    private readonly RawWireConnection _connection;

    private LoginBotClient(RawWireConnection connection)
    {
        _connection = connection;
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
            throw new InvalidOperationException($"Expected LoginGreetingResponse (op {LoginGreetingResponse.Opcode}), got op {frame[0]}.");

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
            throw new InvalidOperationException($"Expected LoginResponse (op {LoginResponse.Opcode}), got op {loginRecvFrame[0]}.");
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

    private const int LoginTrainAvatarSlotCount = 3;

    /// <summary>op13 CL_CREATE_MOUSE_PASSWORD_SEND -- only legal from PinRequired (fresh account, no stored PIN yet).</summary>
    public async Task<int> CreateMousePinAsync(string pin, CancellationToken ct)
    {
        var payload = new byte[CreateMousePinRequest.PayloadSize];
        WireScalars.WriteFixedString(payload.AsSpan(0, 5), pin);
        await SendAsync(CreateMousePinRequest.Opcode, payload, ct);

        var frame = await _connection.ReadExactAsync(1 + CreateMousePinResponse.PayloadSize, ct);
        if (frame[0] != CreateMousePinResponse.Opcode)
            throw new InvalidOperationException($"Expected CreateMousePinResponse (op {CreateMousePinResponse.Opcode}), got op {frame[0]}.");
        return WireScalars.ReadInt32(frame.AsSpan(1, 4));
    }

    /// <summary>op17 CL_CREATE_AVATAR_SEND2.</summary>
    public async Task<int> CreateAvatarAsync(int avatarPost, int tribe, int gender, int head, int face,
        string avatarName, CancellationToken ct)
    {
        var payload = new byte[CreateAvatarRequest.PayloadSize];
        WireScalars.WriteInt32(payload.AsSpan(0, 4), avatarPost);
        WireScalars.WriteInt32(payload.AsSpan(4, 4), tribe);
        WireScalars.WriteInt32(payload.AsSpan(8, 4), 0); // PreviousTribe
        WireScalars.WriteInt32(payload.AsSpan(12, 4), gender);
        WireScalars.WriteInt32(payload.AsSpan(16, 4), head);
        WireScalars.WriteInt32(payload.AsSpan(20, 4), face);
        WireScalars.WriteInt32(payload.AsSpan(24, 4), 0); // Weapon -- dropped server-side, no starting-weapon system
        WireScalars.WriteFixedString(payload.AsSpan(28, 13), avatarName);
        await SendAsync(CreateAvatarRequest.Opcode, payload, ct);

        var frame = await _connection.ReadExactAsync(1 + CreateAvatarResponse.PayloadSize, ct);
        if (frame[0] != CreateAvatarResponse.Opcode)
            throw new InvalidOperationException($"Expected CreateAvatarResponse (op {CreateAvatarResponse.Opcode}), got op {frame[0]}.");
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
            throw new InvalidOperationException($"Expected ZoneTransferResponse (op {ZoneTransferResponse.Opcode}), got op {frame[0]}.");

        var result = WireScalars.ReadInt32(frame.AsSpan(1, 4));
        var ip = WireScalars.ReadFixedString(frame.AsSpan(5, 16));
        var port = WireScalars.ReadInt32(frame.AsSpan(21, 4));
        var zone = WireScalars.ReadInt32(frame.AsSpan(25, 4));
        return new ZoneTransferResult(result, ip, port, zone);
    }

    private async Task SendAsync(byte opcode, byte[] payload, CancellationToken ct)
    {
        var frame = new byte[1 + payload.Length];
        frame[0] = opcode;
        payload.CopyTo(frame.AsSpan(1));
        await _connection.SendAsync(frame, ct);
    }

    public ValueTask DisposeAsync()
    {
        return _connection.DisposeAsync();
    }
}

public readonly record struct LoginResult(int Result, int SecondLoginSort);

public readonly record struct ZoneTransferResult(int Result, string Ip, int Port, int Zone);
