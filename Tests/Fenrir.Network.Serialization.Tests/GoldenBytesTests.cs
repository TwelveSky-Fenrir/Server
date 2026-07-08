using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Compression;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests;

// Byte vectors are hand-built from Docs/protocol/M1_Legacy_Wire_Contract.md, independent of the Write/TryRead under test.
public class GoldenBytesTests
{
    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }

    [Fact]
    public void LcLoginConnectOkRecv_GoldenBytes_AndPacketXor()
    {
        // tPad0..tPad4 (offsets 0..19) are dead residue, zeroed on the wire; real fields start at offset 20.
        var packet = new LoginGreetingResponse
        {
            RandomNumber = 0x1A2B3C4D,
            MaxPlayerNum = 1000,
            GagePlayerNum = 500,
            PresentPlayerNum = 250
        };

        Assert.Equal(36, LoginGreetingResponse.PayloadSize);

        var payload = new byte[LoginGreetingResponse.PayloadSize];
        var written = packet.Write(payload);
        Assert.Equal(36, written);

        var expectedPayload = new byte[36];
        BinaryPrimitives.WriteInt32LittleEndian(expectedPayload.AsSpan(20, 4), 0x1A2B3C4D);
        BinaryPrimitives.WriteInt32LittleEndian(expectedPayload.AsSpan(24, 4), 1000);
        BinaryPrimitives.WriteInt32LittleEndian(expectedPayload.AsSpan(28, 4), 500);
        BinaryPrimitives.WriteInt32LittleEndian(expectedPayload.AsSpan(32, 4), 250);

        Assert.Equal(expectedPayload, payload);

        // XOR_PACKET: buf[0]^=0x10, buf[1..size-2]^=0xFE, buf[size-1] untouched (simulated here on the full frame).
        var frame = new byte[1 + payload.Length];
        frame[0] = LoginGreetingResponse.Opcode;
        payload.CopyTo(frame, 1);
        var lastByteBeforeXor = frame[^1];

        WireXor.ApplyPacketXor(frame);

        Assert.Equal((byte)(LoginGreetingResponse.Opcode ^ 0x10), frame[0]);
        Assert.Equal(lastByteBeforeXor, frame[^1]);
    }

    [Fact]
    public void ClLoginSend_GoldenBytes_TryRead()
    {
        Assert.Equal(448, LoginRequest.PayloadSize);

        var payload = new byte[LoginRequest.PayloadSize];
        WriteFixedString(payload.AsSpan(0, 255), "GoldenUser");
        WriteFixedString(payload.AsSpan(255, 33), "Secr3t");
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(288, 4), 42);
        WriteFixedString(payload.AsSpan(292, 128), "eth0");
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(420, 4), 6);
        new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x00, 0x00 }.CopyTo(payload.AsSpan(424, 8));
        WriteFixedString(payload.AsSpan(432, 16), "192.168.1.10");

        Assert.True(LoginRequest.TryRead(payload, out var packet));
        Assert.Equal("GoldenUser", packet.Id);
        Assert.Equal("Secr3t", packet.Password);
        Assert.Equal(42, packet.Version);
        Assert.Equal("eth0", packet.Adapter.AdapterName);
        Assert.Equal(6u, packet.Adapter.PhysicalAddressLength);
        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x00, 0x00 }, packet.Adapter.PhysicalAddress);
        Assert.Equal("192.168.1.10", packet.Adapter.IPAddress);
    }

    [Fact]
    public void LcDemandZoneServerInfoRecv_GoldenBytes_Write()
    {
        var packet = new ZoneTransferResponse
        {
            Result = 1,
            Ip = "10.0.0.5",
            Port = 7100,
            Zone = 3
        };

        Assert.Equal(28, ZoneTransferResponse.PayloadSize);

        var payload = new byte[ZoneTransferResponse.PayloadSize];
        var written = packet.Write(payload);
        Assert.Equal(28, written);

        var expected = new byte[28];
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(0, 4), 1);
        WriteFixedString(expected.AsSpan(4, 16), "10.0.0.5");
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(20, 4), 7100);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(24, 4), 3);

        Assert.Equal(expected, payload);
    }

    [Fact]
    public void ZcConnectOkRecv_GoldenBytes_Write()
    {
        // RandomNumber is the plaintext stream seed; out of Contracts' scope to XOR it.
        var packet = new ZoneGreetingResponse { RandomNumber = unchecked(0x77AABBCC) };

        Assert.Equal(4, ZoneGreetingResponse.PayloadSize);

        var payload = new byte[ZoneGreetingResponse.PayloadSize];
        var written = packet.Write(payload);
        Assert.Equal(4, written);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, unchecked(0x77AABBCC));

        Assert.Equal(expected, payload);
    }

    [Fact]
    public void CzRegisterAvatarSend_GoldenBytes_TryRead()
    {
        Assert.Equal(372, EnterWorldRequest.PayloadSize);

        var payload = new byte[EnterWorldRequest.PayloadSize];
        WriteFixedString(payload.AsSpan(0, 255), "Hero");
        WriteFixedString(payload.AsSpan(255, 13), "HeroAvatar");

        const int actionOffset = 268;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(actionOffset + 0, 4), 1); // Type
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(actionOffset + 4, 4), 2); // Sort
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 8, 4), 3.5f); // Frame
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 12, 4), 1f); // Location[0]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 16, 4), 2f); // Location[1]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 20, 4), 3f); // Location[2]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 24, 4), 4f); // TargetLocation[0]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 28, 4), 5f); // TargetLocation[1]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 32, 4), 6f); // TargetLocation[2]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 36, 4), 30.5f); // Front
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 40, 4), 31.5f); // TargetFront
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 44, 4), 40f); // PetLocation[0]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 48, 4), 41f); // PetLocation[1]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 52, 4), 42f); // PetLocation[2]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 56, 4), 50f); // PetTargetLocation[0]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 60, 4), 51f); // PetTargetLocation[1]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 64, 4), 52f); // PetTargetLocation[2]
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(actionOffset + 68, 4), 60.5f); // PetFront
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(actionOffset + 72, 4), 70); // PetSort
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(actionOffset + 76, 4), 71); // TargetObjectSort
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(actionOffset + 80, 4), 72); // TargetObjectIndex
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(actionOffset + 84, 4), 73); // TargetObjectUniqueNumber
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(actionOffset + 88, 4), 74); // SkillNumber
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(actionOffset + 92, 4), 75); // SkillGradeNum1
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(actionOffset + 96, 4), 76); // SkillGradeNum2
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(actionOffset + 100, 4), 77); // SkillValue

        Assert.True(EnterWorldRequest.TryRead(payload, out var packet));
        Assert.Equal("Hero", packet.Id);
        Assert.Equal("HeroAvatar", packet.AvatarName);
        Assert.Equal(1, packet.Action.Type);
        Assert.Equal(2, packet.Action.Sort);
        Assert.Equal(3.5f, packet.Action.Frame);
        Assert.Equal(new[] { 1f, 2f, 3f }, packet.Action.Location);
        Assert.Equal(new[] { 4f, 5f, 6f }, packet.Action.TargetLocation);
        Assert.Equal(30.5f, packet.Action.Front);
        Assert.Equal(31.5f, packet.Action.TargetFront);
        Assert.Equal(new[] { 40f, 41f, 42f }, packet.Action.PetLocation);
        Assert.Equal(new[] { 50f, 51f, 52f }, packet.Action.PetTargetLocation);
        Assert.Equal(60.5f, packet.Action.PetFront);
        Assert.Equal(70, packet.Action.PetSort);
        Assert.Equal(71, packet.Action.TargetObjectSort);
        Assert.Equal(72, packet.Action.TargetObjectIndex);
        Assert.Equal(73, packet.Action.TargetObjectUniqueNumber);
        Assert.Equal(74, packet.Action.SkillNumber);
        Assert.Equal(75, packet.Action.SkillGradeNum1);
        Assert.Equal(76, packet.Action.SkillGradeNum2);
        Assert.Equal(77, packet.Action.SkillValue);
    }

    [Fact]
    public void ZcAvatarActionRecv_GoldenBytes_Write()
    {
        // OBJECT_FOR_AVATAR has five 3-byte natural-alignment padding gaps (the Skip(3) calls below).
        var action = new ActionInfo
        {
            Type = 500,
            Sort = 501,
            Frame = 1.5f,
            Location = new[] { 2f, 3f, 4f },
            TargetLocation = new[] { 5f, 6f, 7f },
            Front = 8.5f,
            TargetFront = 9.5f,
            PetLocation = new[] { 10f, 11f, 12f },
            PetTargetLocation = new[] { 13f, 14f, 15f },
            PetFront = 16.5f,
            PetSort = 502,
            TargetObjectSort = 503,
            TargetObjectIndex = 504,
            TargetObjectUniqueNumber = 505,
            SkillNumber = 506,
            SkillGradeNum1 = 507,
            SkillGradeNum2 = 508,
            SkillValue = 509
        };

        var equipForView = new int[26];
        for (var i = 0; i < equipForView.Length; i++)
            equipForView[i] = 100 + i;

        var effectValueForView = new int[35];
        for (var i = 0; i < effectValueForView.Length; i++)
            effectValueForView[i] = 200 + i;

        var data = new ObjectForAvatar
        {
            VisibleState = 1,
            SpecialState = 2,
            KillOtherTribe = 3,
            GoodFellow = 4,
            GuildName = "Guild1",
            GuildRole = 5,
            CallName = "Cal1",
            GuildMarkEffect = 6,
            Name = "Hero1",
            Tribe = 7,
            PreviousTribe = 8,
            Gender = 9,
            HeadType = 10,
            FaceType = 11,
            Level1 = 12,
            Level2 = 13,
            EquipForView = equipForView,
            AnimalNumber = 14,
            Title = 15,
            Halo = 16,
            RebirthNum = 17,
            BattleTeam = 18,
            Action = action,
            MaxLifeValue = 19,
            LifeValue = 20,
            MaxManaValue = 21,
            ManaValue = 22,
            EffectValueForView = effectValueForView,
            PartyName = "Party1",
            DuelState = new[] { 1, 2, 3 },
            PShopState = 23,
            PShopName = "Shop1",
            CostumeNumber = 24,
            BufEffectTimeState = 25,
            BufSort = 26,
            AutoState = 27,
            FishingState = 28,
            FishingStep = 29,
            FishingPoint = new[] { 1.1f, 2.2f, 3.3f },
            RankPoint = 30,
            TargetState = 31,
            AnimalAbsorbState = 32,
            PetValid = 33,
            Unk1 = 34,
            PetLocation = new[] { 4.4f, 5.5f, 6.6f },
            PetFrame = 7.7f,
            Unk624 = 35,
            Unk625 = 36,
            UniqueSkillNumber = 37,
            UniqueSkillBuffTime = 38,
            CostumeState = 39,
            StellarCoreNumber = 40
        };

        var packet = new AvatarActionResponse
        {
            ServerIndex = 777,
            UniqueNumber = 0xCAFEBABEu,
            Data = data,
            CheckChangeActionState = 999
        };

        Assert.Equal(644, AvatarActionResponse.PayloadSize);

        var payload = new byte[AvatarActionResponse.PayloadSize];
        var written = packet.Write(payload);
        Assert.Equal(644, written);

        var expected = new byte[644];
        var cursor = 0;

        void WriteInt(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(cursor, 4), value);
            cursor += 4;
        }

        void WriteUInt(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(cursor, 4), value);
            cursor += 4;
        }

        void WriteFloat(float value)
        {
            BinaryPrimitives.WriteSingleLittleEndian(expected.AsSpan(cursor, 4), value);
            cursor += 4;
        }

        void WriteIntArray(int[] values)
        {
            foreach (var value in values)
                WriteInt(value);
        }

        void WriteFloatArray(float[] values)
        {
            foreach (var value in values)
                WriteFloat(value);
        }

        void WriteString(int fieldLength, string value)
        {
            WriteFixedString(expected.AsSpan(cursor, fieldLength), value);
            cursor += fieldLength;
        }

        void Skip(int count)
        {
            cursor += count;
        }

        WriteInt(777);
        WriteUInt(0xCAFEBABEu);

        WriteInt(1);
        WriteInt(2);
        WriteInt(3);
        WriteInt(4);
        WriteString(13, "Guild1");
        Skip(3);
        WriteInt(5);
        WriteString(5, "Cal1");
        Skip(3);
        WriteInt(6);
        WriteString(13, "Hero1");
        Skip(3);
        WriteInt(7);
        WriteInt(8);
        WriteInt(9);
        WriteInt(10);
        WriteInt(11);
        WriteInt(12);
        WriteInt(13);
        WriteIntArray(equipForView);
        WriteInt(14);
        WriteInt(15);
        WriteInt(16);
        WriteInt(17);
        WriteInt(18);

        WriteInt(500);
        WriteInt(501);
        WriteFloat(1.5f);
        WriteFloatArray(new[] { 2f, 3f, 4f });
        WriteFloatArray(new[] { 5f, 6f, 7f });
        WriteFloat(8.5f);
        WriteFloat(9.5f);
        WriteFloatArray(new[] { 10f, 11f, 12f });
        WriteFloatArray(new[] { 13f, 14f, 15f });
        WriteFloat(16.5f);
        WriteInt(502);
        WriteInt(503);
        WriteInt(504);
        WriteInt(505);
        WriteInt(506);
        WriteInt(507);
        WriteInt(508);
        WriteInt(509);

        WriteInt(19);
        WriteInt(20);
        WriteInt(21);
        WriteInt(22);
        WriteIntArray(effectValueForView);
        WriteString(13, "Party1");
        Skip(3);
        WriteIntArray(new[] { 1, 2, 3 });
        WriteInt(23);
        WriteString(25, "Shop1");
        Skip(3);
        WriteInt(24);
        WriteInt(25);
        WriteInt(26);
        WriteInt(27);
        WriteInt(28);
        WriteInt(29);
        WriteFloatArray(new[] { 1.1f, 2.2f, 3.3f });
        WriteInt(30);
        WriteInt(31);
        WriteInt(32);
        WriteInt(33);
        WriteInt(34);
        WriteFloatArray(new[] { 4.4f, 5.5f, 6.6f });
        WriteFloat(7.7f);
        WriteInt(35);
        WriteInt(36);
        WriteInt(37);
        WriteInt(38);
        WriteInt(39);
        WriteInt(40);

        WriteInt(999);

        Assert.Equal(644, cursor);
        Assert.Equal(expected, payload);
    }
}
