using System.Buffers.Binary;
using System.Collections.Concurrent;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Compression;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using K4os.Compression.LZ4;

namespace Fenrir.IntegrationTests.Wire;

/// <summary>
///     Drives Fenrir.GameServer's wire protocol over a real socket: handshake -&gt; enter world -&gt; ready, then
///     a background pump keeps draining unsolicited broadcasts (monster/ground-item replication, attack
///     results, ...) into small snapshots the scenario test can poll, while <see cref="AttackAsync" />/
///     <see cref="MoveAsync" />/etc. keep sending. See WireScalars's doc comment for why top-level packet
///     fields are hand-built/hand-parsed; nested [FenrirWireType] sub-structs use their own real
///     Write/TryRead.
/// </summary>
public sealed class ZoneBotClient : IAsyncDisposable
{
    private readonly ConcurrentQueue<AttackForProtocol> _attackResults = new();
    private readonly RawWireConnection _connection;
    private readonly ConcurrentQueue<GenericActionResult> _genericActionResults = new();
    private readonly CancellationTokenSource _pumpCts = new();
    private readonly ConcurrentQueue<int> _zoneMoveResults = new();
    public readonly ConcurrentDictionary<int, GroundItemSnapshot> GroundItems = new();

    public readonly ConcurrentDictionary<int, MonsterSnapshot> Monsters = new();
    private Task? _pumpTask;

    private ZoneBotClient(RawWireConnection connection)
    {
        _connection = connection;
    }

    public async ValueTask DisposeAsync()
    {
        await _pumpCts.CancelAsync();
        if (_pumpTask is not null)
            try
            {
                await _pumpTask;
            }
            catch
            {
                // Pump loop already swallows its own expected faults; a leftover exception here is never fatal to teardown.
            }

        _pumpCts.Dispose();
        await _connection.DisposeAsync();
    }

    public static async Task<ZoneBotClient> ConnectAsync(int port, CancellationToken ct)
    {
        var connection = await RawWireConnection.ConnectAsync(port, ct);
        var bot = new ZoneBotClient(connection);
        await bot.ReadGreetingAsync(ct);
        return bot;
    }

    /// <summary>op0 ZC_CONNECT_OK_RECV -- unlike Login's, carries no packet-level XOR, only seeds the stream cipher.</summary>
    private async Task ReadGreetingAsync(CancellationToken ct)
    {
        var frame = await _connection.ReadExactAsync(1 + ZoneGreetingResponse.PayloadSize, ct);
        if (frame[0] != ZoneGreetingResponse.Opcode)
            throw new InvalidOperationException(
                $"Expected ZoneGreetingResponse (op {ZoneGreetingResponse.Opcode}), got op {frame[0]}.");

        var randomNumber = WireScalars.ReadInt32(frame.AsSpan(1, 4));
        _connection.SeedOutboundStreamKey(randomNumber);
    }

    /// <summary>op11 CL_ZONE_INFO_SEND -- consumes the single-use ticket ZoneTransferHandler minted.</summary>
    public async Task<int> HandshakeAsync(int accountId, int tribe, int userSort, CancellationToken ct)
    {
        var payload = new byte[ZoneHandshakeRequest.PayloadSize];
        WriteObfuscatedAccountId(payload.AsSpan(0, 255), accountId);
        WireScalars.WriteInt32(payload.AsSpan(255, 4), tribe);
        WireScalars.WriteInt32(payload.AsSpan(259, 4), userSort);
        await SendAsync(ZoneHandshakeRequest.Opcode, payload, ct);

        var frame = await _connection.ReadExactAsync(1 + ZoneHandshakeResponse.PayloadSize, ct);
        if (frame[0] != ZoneHandshakeResponse.Opcode)
            throw new InvalidOperationException(
                $"Expected ZoneHandshakeResponse (op {ZoneHandshakeResponse.Opcode}), got op {frame[0]}.");
        return WireScalars.ReadInt32(frame.AsSpan(1, 4));
    }

    /// <summary>
    ///     op12 CL_REGISTER_AVATAR_SEND. Reads the full self-registration train: compressed EnterWorldResponse,
    ///     compressed WorldSnapshotResponse, the (uncompressed, plain <c>Send</c>) TowerStatusResponse legacy
    ///     pairs with that same broadcast unconditionally for every zone entry (see EnterWorldService's own
    ///     remarks -- Server/ts25zone/S04_MyWork02.cpp:1203-1204, not gated on this being a tower zone), then
    ///     the self-spawn AvatarActionResponse (see EnterWorldHandler).
    /// </summary>
    public async Task<EnterWorldResult> EnterWorldAsync(int accountId, string avatarName, CancellationToken ct)
    {
        var payload = new byte[EnterWorldRequest.PayloadSize];
        WriteObfuscatedAccountId(payload.AsSpan(0, 255), accountId);
        WireScalars.WriteFixedString(payload.AsSpan(255, 13), avatarName);
        ZeroedAction().Write(payload.AsSpan(268, ActionInfo.WireSize));
        await SendAsync(EnterWorldRequest.Opcode, payload, ct);

        var enterWorldPayload = await ReadCompressedPayloadAsync(Opcodes.Zone.Outgoing.EnterWorld, ct);
        if (!AvatarInfo.TryRead(enterWorldPayload.AsSpan(0, AvatarInfo.WireSize), out var avatarInfo))
            throw new InvalidOperationException("Failed to decode AvatarInfo from EnterWorldResponse.");

        await ReadCompressedPayloadAsync(Opcodes.Zone.Outgoing.WorldSnapshot, ct); // WorldSnapshotResponse, unused

        var towerStatusFrame = await _connection.ReadExactAsync(1 + TowerStatusResponse.PayloadSize, ct);
        if (towerStatusFrame[0] != TowerStatusResponse.Opcode)
            throw new InvalidOperationException(
                $"Expected TowerStatusResponse (op {TowerStatusResponse.Opcode}), got op {towerStatusFrame[0]}.");

        var selfSpawnFrame = await _connection.ReadExactAsync(1 + AvatarActionResponse.PayloadSize, ct);
        if (selfSpawnFrame[0] != AvatarActionResponse.Opcode)
            throw new InvalidOperationException(
                $"Expected self-spawn AvatarActionResponse (op {AvatarActionResponse.Opcode}), got op {selfSpawnFrame[0]}.");
        var selfServerIndex = WireScalars.ReadInt32(selfSpawnFrame.AsSpan(1, 4));
        var selfUniqueNumber = WireScalars.ReadUInt32(selfSpawnFrame.AsSpan(5, 4));
        if (!ObjectForAvatar.TryRead(selfSpawnFrame.AsSpan(9, ObjectForAvatar.WireSize), out var selfObject))
            throw new InvalidOperationException(
                "Failed to decode ObjectForAvatar from self-spawn AvatarActionResponse.");

        return new EnterWorldResult(avatarInfo, selfServerIndex, selfUniqueNumber, selfObject);
    }

    /// <summary>op13 CL_MOVE_ZONE_OK_SEND -- final ack, no reply, session becomes InWorld. Safe to start the pump right after.</summary>
    public async Task ReadyAsync(int tribe, CancellationToken ct)
    {
        var payload = new byte[ZoneReadyRequest.PayloadSize];
        WireScalars.WriteInt32(payload.AsSpan(0, 4), tribe);
        WireScalars.WriteInt32(payload.AsSpan(4, 4), 0); // AutoTime
        WireScalars.WriteInt32(payload.AsSpan(8, 4), 0); // AutoTime2
        WireScalars.WriteInt32(payload.AsSpan(12, 4), 0); // AutoState
        await SendAsync(ZoneReadyRequest.Opcode, payload, ct);
    }

    public void StartBackgroundPump()
    {
        _pumpTask = Task.Run(() => PumpLoopAsync(_pumpCts.Token));
    }

    private async Task PumpLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var opcode = (await _connection.ReadExactAsync(1, ct))[0];

                if (opcode is Opcodes.Zone.Outgoing.EnterWorld or Opcodes.Zone.Outgoing.WorldSnapshot)
                {
                    await ReadCompressedBodyAsync(
                        ct); // periodic full re-snapshot, if the server ever sends one; discarded
                    continue;
                }

                var frameSize = ZoneOpcodeRegistry.FrameSizeOf(FenrirServer.Zone, FenrirDirection.Outgoing, opcode);
                var payload = await _connection.ReadExactAsync(frameSize - 1, ct);
                Dispatch(opcode, payload);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
            // Peer closed (e.g. test disposed the connection) -- the pump's job ends with the socket.
        }
    }

    private void Dispatch(byte opcode, byte[] payload)
    {
        if (opcode == Opcodes.Zone.Outgoing.MonsterReplication)
        {
            var serverIndex = WireScalars.ReadInt32(payload.AsSpan(0, 4));
            var uniqueNumber = WireScalars.ReadUInt32(payload.AsSpan(4, 4));
            if (!ObjectForMonster.TryRead(payload.AsSpan(8, ObjectForMonster.WireSize), out var data))
                return;
            Monsters[serverIndex] = new MonsterSnapshot(serverIndex, uniqueNumber, data);
        }
        else if (opcode == Opcodes.Zone.Outgoing.GroundItemReplication)
        {
            var serverIndex = WireScalars.ReadInt32(payload.AsSpan(0, 4));
            var uniqueNumber = WireScalars.ReadUInt32(payload.AsSpan(4, 4));
            if (!ObjectForItem.TryRead(payload.AsSpan(8, ObjectForItem.WireSize), out var data))
                return;
            // No despawn/removal tracking: the scenario only needs "a ground item appeared near me", picked up
            // by ServerIndex/UniqueNumber, and TryClaimGroundItem itself is the authority on whether it's still there.
            GroundItems[serverIndex] = new GroundItemSnapshot(serverIndex, uniqueNumber, data);
        }
        else if (opcode == Opcodes.Zone.Outgoing.Attack)
        {
            if (AttackForProtocol.TryRead(payload, out var attackInfo))
                _attackResults.Enqueue(attackInfo);
        }
        else if (opcode == Opcodes.Zone.Outgoing.GenericAction)
        {
            var result = WireScalars.ReadInt32(payload.AsSpan(0, 4));
            var sort = WireScalars.ReadInt32(payload.AsSpan(4, 4));
            _genericActionResults.Enqueue(new GenericActionResult(result, sort));
        }
        else if (opcode == Opcodes.Zone.Outgoing.ZoneMove)
        {
            var result = WireScalars.ReadInt32(payload.AsSpan(0, 4));
            _zoneMoveResults.Enqueue(result);
        }
    }

    public bool TryTakeAttackResult(out AttackForProtocol result)
    {
        return _attackResults.TryDequeue(out result);
    }

    public bool TryTakeGenericActionResult(out GenericActionResult result)
    {
        return _genericActionResults.TryDequeue(out result);
    }

    public bool TryTakeZoneMoveResult(out int result)
    {
        return _zoneMoveResults.TryDequeue(out result);
    }

    /// <summary>op15 CZ_AVATAR_ACTION_SEND -- movement AND every other unified avatar action (sit/skill/...).</summary>
    public async Task MoveAsync(float x, float y, float z, float heading, CancellationToken ct)
    {
        var payload = new byte[AvatarActionRequest.PayloadSize];
        var action = new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [x, y, z],
            TargetLocation = [x, y, z],
            Front = heading,
            TargetFront = heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };
        action.Write(payload);
        await SendAsync(AvatarActionRequest.Opcode, payload, ct);
    }

    /// <summary>op18 CZ_PROCESS_ATTACK_SEND. mCase 3 = Avatar -&gt; Monster (see MonsterCombatResolver.ResolvePvmAttack).</summary>
    /// <remarks>
    ///     Declares a legal single-hit swing (op15 CZ_AVATAR_ACTION_SEND, Sort 5) immediately before the
    ///     attack sub-packet itself -- <c>Zone.ApplyPvmAttack</c> now enforces
    ///     <c>AttackPacketBudget.TryConsume</c> (mirrors <c>CheckAttackPacket</c>,
    ///     Server/ts25zone/S07_MyGame02.cpp:1718-1761), which silently drops any mCase 3 sub-packet unless a
    ///     prior avatar action already established a non-zero ceiling and a matching replay-guard value. One
    ///     declare per call keeps this bot's retry loop (<c>KillMonsterUntilDeadAsync</c>) always within
    ///     budget without needing to track cross-call state; the declared position mirrors the caller's own
    ///     current position, so it is never rejected as an implausible move.
    /// </remarks>
    public async Task AttackMonsterAsync(int attackerServerIndex, uint attackerUniqueNumber, int monsterServerIndex,
        uint monsterUniqueNumber, float x, float y, float z, CancellationToken ct)
    {
        const int singleHitSwingSort = 5;

        var declarePayload = new byte[AvatarActionRequest.PayloadSize];
        var declareAction = new ActionInfo
        {
            Type = 1,
            Sort = singleHitSwingSort,
            Frame = 0,
            Location = [x, y, z],
            TargetLocation = [x, y, z],
            Front = 0,
            TargetFront = 0,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };
        declareAction.Write(declarePayload);
        await SendAsync(AvatarActionRequest.Opcode, declarePayload, ct);

        var payload = new byte[AttackRequest.PayloadSize];
        var info = new AttackForProtocol
        {
            Case = 3,
            ServerIndex1 = attackerServerIndex,
            UniqueNumber1 = attackerUniqueNumber,
            ServerIndex2 = monsterServerIndex,
            UniqueNumber2 = monsterUniqueNumber,
            SenderLocation = [x, y, z],
            AttackActionValue1 = 1,
            AttackActionValue2 = 0,
            AttackActionValue3 = 0,
            AttackActionValue4 = singleHitSwingSort,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
        info.Write(payload);
        await SendAsync(AttackRequest.Opcode, payload, ct);
    }

    /// <summary>
    ///     op19 CZ_PROCESS_DATA_SEND, tSort 201 -- ground pickup (Page1/Index1 repurposed as the item's
    ///     ServerIndex/UniqueNumber).
    /// </summary>
    public async Task PickupGroundItemAsync(int itemServerIndex, uint itemUniqueNumber, byte destinationContainer,
        byte destinationSlot, CancellationToken ct)
    {
        var move = new DefaultPData
        {
            Page1 = itemServerIndex,
            Index1 = unchecked((int)itemUniqueNumber),
            Quantity1 = 0,
            Page2 = destinationContainer,
            Index2 = destinationSlot,
            XPost2 = 0,
            YPost2 = 0
        };
        await SendGenericActionAsync(201, move, ct);
    }

    /// <summary>op19 CZ_PROCESS_DATA_SEND, tSort 215 -- NPC shop buy (Page1/Index1 repurposed as NpcId/ItemId).</summary>
    public async Task BuyFromNpcShopAsync(int npcId, int itemId, int quantity, byte destinationContainer,
        byte destinationSlot, CancellationToken ct)
    {
        var move = new DefaultPData
        {
            Page1 = npcId,
            Index1 = itemId,
            Quantity1 = quantity,
            Page2 = destinationContainer,
            Index2 = destinationSlot,
            XPost2 = 0,
            YPost2 = 0
        };
        await SendGenericActionAsync(215, move, ct);
    }

    private async Task SendGenericActionAsync(int sort, DefaultPData move, CancellationToken ct)
    {
        var payload = new byte[GenericActionRequest.PayloadSize];
        WireScalars.WriteInt32(payload.AsSpan(0, 4), sort);
        var data = new byte[130];
        move.Write(data.AsSpan(0, DefaultPData.WireSize));
        data.CopyTo(payload.AsSpan(4, 130));
        await SendAsync(GenericActionRequest.Opcode, payload, ct);
    }

    /// <summary>
    ///     op19 CZ_PROCESS_DATA_SEND, tSort 206 -- ProcessForStatPlus. STAT_PLUS_RECV is a bare two-int payload
    ///     (tStatSort then tAddValue, back-to-back at offset 0) rather than DefaultPData's 7-field/28-byte shape
    ///     -- see GenericActionHandler's own sort-206 remarks and StatAllocationResolver for the category-code
    ///     table (9-12 = variable-regime Str/Dex/Vit/Int, crediting exactly <paramref name="addValue" /> and
    ///     debiting the same amount from the character's StatPoints balance).
    /// </summary>
    public async Task AllocateStatPointAsync(int statSort, int addValue, CancellationToken ct)
    {
        var payload = new byte[GenericActionRequest.PayloadSize];
        WireScalars.WriteInt32(payload.AsSpan(0, 4), 206);
        var data = new byte[130];
        WireScalars.WriteInt32(data.AsSpan(0, 4), statSort);
        WireScalars.WriteInt32(data.AsSpan(4, 4), addValue);
        data.CopyTo(payload.AsSpan(4, 130));
        await SendAsync(GenericActionRequest.Opcode, payload, ct);
    }

    /// <summary>op20 CZ_DEMAND_ZONE_SERVER_INFO_2 -- in-process zone transfer (ZoneMoveHandler); Sort 4 = portal.</summary>
    public async Task ZoneMoveAsync(int targetZoneNumber, int presentZoneNumber, CancellationToken ct)
    {
        var payload = new byte[ZoneMoveRequest.PayloadSize];
        WireScalars.WriteInt32(payload.AsSpan(0, 4), 4); // Sort=4 (portal)
        WireScalars.WriteInt32(payload.AsSpan(4, 4), targetZoneNumber);
        WireScalars.WriteInt32(payload.AsSpan(8, 4), presentZoneNumber);
        await SendAsync(ZoneMoveRequest.Opcode, payload, ct);
    }

    /// <summary>op38 CZ_LOCAL_CHAT_SEND.</summary>
    public async Task LocalChatAsync(string content, CancellationToken ct)
    {
        var payload = new byte[LocalChatRequest.PayloadSize];
        WireScalars.WriteFixedString(payload.AsSpan(0, 61), content);
        new ItemLinkInfo { Index = 0, Activity = 0, Value = 0, Socket = new int[3] }
            .Write(payload.AsSpan(61, ItemLinkInfo.WireSize));
        await SendAsync(LocalChatRequest.Opcode, payload, ct);
    }

    private static ActionInfo ZeroedAction()
    {
        return new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = new float[3],
            TargetLocation = new float[3],
            Front = 0,
            TargetFront = 0,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };
    }

    /// <summary>Mirrors ObfuscatedUidCodec.TryDecodeAccountId's encoding half: Latin1("MG"+id), then USE_XOR_UID.</summary>
    private static void WriteObfuscatedAccountId(Span<byte> destination, int accountId)
    {
        WireScalars.WriteFixedString(destination, "MG" + accountId);
        WireXor.ApplyUidXor(destination);
    }

    private async Task<byte[]> ReadCompressedPayloadAsync(byte expectedOpcode, CancellationToken ct)
    {
        var opcode = (await _connection.ReadExactAsync(1, ct))[0];
        if (opcode != expectedOpcode)
            throw new InvalidOperationException($"Expected compressed opcode {expectedOpcode}, got {opcode}.");
        return await ReadCompressedBodyAsync(ct);
    }

    /// <summary>ZC_ZPACKET_OK_SEND envelope (§3.5): isCompress(4) + originalSize(4) + compressSize(4) + payload.</summary>
    private async Task<byte[]> ReadCompressedBodyAsync(CancellationToken ct)
    {
        var header = await _connection.ReadExactAsync(12, ct);
        var isCompress = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var originalSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        var compressSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8, 4));

        var bodySize = isCompress != 0 ? compressSize : originalSize;
        var body = await _connection.ReadExactAsync(bodySize, ct);

        if (isCompress == 0)
            return body;

        var decoded = new byte[originalSize];
        var written = LZ4Codec.Decode(body, decoded);
        if (written != originalSize)
            throw new InvalidOperationException($"LZ4 decode produced {written} bytes, expected {originalSize}.");
        return decoded;
    }

    /// <summary>
    ///     Every client-&gt;server frame is <c>CLIENT_PACKET</c>-framed on the wire (<c>WireHeaderSizes.ClientPacketSize</c>
    ///     = 9: <c>int tPacket1 + int tPacket2 + BYTE tProtocol</c>, opcode at header offset 8), not the bare
    ///     opcode-then-payload shape a server response uses -- <see cref="Fenrir.Network.Framing.FrameReader" />
    ///     unconditionally requires and slices off those leading 9 bytes before locating the opcode/payload for
    ///     any INCOMING frame, on both LoginServer and GameServer. tPacket1/tPacket2 carry no framing
    ///     information the server ever validates (see FrameReader's own remarks), so any filler value works --
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

public readonly record struct EnterWorldResult(
    AvatarInfo AvatarInfo,
    int SelfServerIndex,
    uint SelfUniqueNumber,
    ObjectForAvatar SelfObject);

public readonly record struct MonsterSnapshot(int ServerIndex, uint UniqueNumber, ObjectForMonster Data);

public readonly record struct GroundItemSnapshot(int ServerIndex, uint UniqueNumber, ObjectForItem Data);

public readonly record struct GenericActionResult(int Result, int Sort);
