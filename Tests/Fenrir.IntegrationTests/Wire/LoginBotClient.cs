using Fenrir.Network.Compression;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.IntegrationTests.Wire;

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

        private async Task ReadGreetingAsync(CancellationToken ct)
    {
        var frame = await _connection.ReadExactAsync(1 + LoginGreetingResponse.PayloadSize, ct);
        WireXor.ApplyPacketXor(frame);

        if (frame[0] != LoginGreetingResponse.Opcode)
            throw new InvalidOperationException(
                $"Expected LoginGreetingResponse (op {LoginGreetingResponse.Opcode}), got op {frame[0]}.");

        var randomNumber = WireScalars.ReadInt32(frame.AsSpan(21, 4));
        _connection.SeedOutboundStreamKey(randomNumber);
    }

        public async Task<LoginResult> LoginAsync(string id, string password, int version, CancellationToken ct)
    {
        var payload = new byte[LoginRequest.PayloadSize];
        WireScalars.WriteFixedString(payload.AsSpan(0, 255), id);
        WireScalars.WriteFixedString(payload.AsSpan(255, 33), password);
        WireScalars.WriteInt32(payload.AsSpan(288, 4), version);
        new LoginAdapterInfo
        {
            AdapterName = "integration-test-adapter-guid",
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
        var secondLoginSort = WireScalars.ReadInt32(loginRecvFrame.AsSpan(1 + 4 + 255 + 4 + 4 + 4 + 4, 4));

        for (var i = 0; i < LoginTrainAvatarSlotCount; i++)
            await _connection.ReadExactAsync(1 + AvatarRosterResponse.PayloadSize, ct);

        await _connection.ReadExactAsync(1 + WorldRecommendationResponse.PayloadSize, ct);
        await _connection.ReadExactAsync(1 + WorldRecommendationFinalResponse.PayloadSize, ct);

        return new LoginResult(result, secondLoginSort);
    }

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
