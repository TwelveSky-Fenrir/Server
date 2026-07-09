using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Compression;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class LcUserAvatarRecv2Tests
{
    // --- Golden-buffer helpers -------------------------------------------------------------------------
    // Intentionally re-derived from the ServerDocs-cited scopyAvtXor*/GetMyXor mask description rather than
    // calling into Fenrir.Network.Compression.WireXor, so this test does not validate Write() against the
    // very code it delegates to.

    private const byte GoldenFirstKey = 0x10;
    private const byte GoldenSteadyKey = 0xFE;

    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=4579 (1-byte outbound header) -> 4578-byte payload.
        Assert.Equal(4578, AvatarRosterResponse.PayloadSize);
    }

    // Each field is XORed per its [AvatarXorKind]; unlike USE_XOR_UID these keys aren't content-dependent, so re-applying recovers plaintext.
    [Fact]
    public void RoundTrip_PreservesAllFields_ThroughPerFieldAvatarXor()
    {
        var inventory = Enumerable.Range(1, 768).ToArray();
        var equip = Enumerable.Range(2000, 52).ToArray();
        var logoutInfo = Enumerable.Range(3000, 6).ToArray();
        var storeItem = Enumerable.Range(4000, 224).ToArray();
        var petBag = Enumerable.Range(5000, 20).ToArray();
        var friend = Enumerable.Range(0, 10).Select(i => $"Friend{i}").ToArray();
        var costume = Enumerable.Range(6000, 10).ToArray();

        var packet = new AvatarRosterResponse
        {
            VisibleState = 1,
            SpecialState = 2,
            CostumeIndex = 3,
            Tribe = 4,
            PreviousTribe = 5,
            EatLifePotion = 6,
            Gender = 7,
            HeadType = 8,
            FaceType = 9,
            EatStrPotion = 10,
            Level1 = 11,
            Inventory = inventory,
            Level2 = 12,
            EatManaPotion = 13,
            Halo = 14,
            RebirthNum = 15,
            KillOtherTribe = 16,
            SkillPoint = 17,
            Equip = equip,
            EatDexPotion = 18,
            Name = "PlayerOne",
            EatElePotion = 19,
            LogoutInfo = logoutInfo,
            GuildName = "GuildX",
            StoreItem = storeItem,
            PetBag = petBag,
            Friend = friend,
            Teacher = "TeacherA",
            Student = "StudentB",
            Costume = costume
        };

        var buffer = new byte[AvatarRosterResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(AvatarRosterResponse.PayloadSize, written);

        Assert.Equal(packet.VisibleState, ReadXoredInt(buffer, 0));
        Assert.Equal(packet.SpecialState, ReadXoredInt(buffer, 4));
        Assert.Equal(packet.CostumeIndex, ReadXoredInt(buffer, 8));
        Assert.Equal(packet.Tribe, ReadXoredInt(buffer, 12));
        Assert.Equal(packet.PreviousTribe, ReadXoredInt(buffer, 16));
        Assert.Equal(packet.EatLifePotion, ReadXoredInt(buffer, 20));
        Assert.Equal(packet.Gender, ReadXoredInt(buffer, 24));
        Assert.Equal(packet.HeadType, ReadXoredInt(buffer, 28));
        Assert.Equal(packet.FaceType, ReadXoredInt(buffer, 32));
        Assert.Equal(packet.EatStrPotion, ReadXoredInt(buffer, 36));
        Assert.Equal(packet.Level1, ReadXoredInt(buffer, 40));
        Assert.True(packet.Inventory.SequenceEqual(ReadXoredIntArray(buffer, 44, 768)));
        Assert.Equal(packet.Level2, ReadXoredInt(buffer, 3116));
        Assert.Equal(packet.EatManaPotion, ReadXoredInt(buffer, 3120));
        Assert.Equal(packet.Halo, ReadXoredInt(buffer, 3124));
        Assert.Equal(packet.RebirthNum, ReadXoredInt(buffer, 3128));
        Assert.Equal(packet.KillOtherTribe, ReadXoredInt(buffer, 3132));
        Assert.Equal(packet.SkillPoint, ReadXoredInt(buffer, 3136));
        Assert.True(packet.Equip.SequenceEqual(ReadXoredIntArray(buffer, 3140, 52)));
        Assert.Equal(packet.EatDexPotion, ReadXoredInt(buffer, 3348));
        Assert.Equal(packet.Name, ReadXoredChar(buffer, 3352, 13));
        Assert.Equal(packet.EatElePotion, ReadXoredInt(buffer, 3365));
        Assert.True(packet.LogoutInfo.SequenceEqual(ReadXoredIntArray(buffer, 3369, 6)));
        Assert.Equal(packet.GuildName, ReadXoredChar(buffer, 3393, 13));
        Assert.True(packet.StoreItem.SequenceEqual(ReadXoredIntArray(buffer, 3406, 224)));
        Assert.True(packet.PetBag.SequenceEqual(ReadXoredIntArray(buffer, 4302, 20)));
        Assert.True(packet.Friend.SequenceEqual(ReadXoredChar2Rows(buffer, 4382, 10, 13)));
        Assert.Equal(packet.Teacher, ReadXoredChar(buffer, 4512, 13));
        Assert.Equal(packet.Student, ReadXoredChar(buffer, 4525, 13));
        Assert.True(packet.Costume.SequenceEqual(ReadXoredIntArray(buffer, 4538, 10)));
    }

    // Golden-byte regression test: builds the entire 4578-byte expected payload from scratch (plaintext
    // field values placed at their contract-documented offsets, then masked by a re-implementation of the
    // XOR rules written independently of Fenrir.Network.Compression.WireXor) and asserts one full-buffer
    // equality against Write()'s actual output. RoundTrip_PreservesAllFields_ThroughPerFieldAvatarXor only
    // proves decode(encode(x)) == x per field via the very same WireXor primitives Write() itself calls, so
    // it cannot catch two adjacent fields swapping offsets, a field bleeding into its neighbor's byte
    // range, or the unmasked "spare" bytes (last two bytes of every IntArray/Char field) drifting — this
    // test compares every one of the 4578 bytes against an independently-computed reference sequence.
    [Fact]
    public void Write_ProducesExactGoldenByteSequence_ForEveryAvatarInfoField()
    {
        var inventory = Enumerable.Range(1, 768).ToArray();
        var equip = Enumerable.Range(2000, 52).ToArray();
        var logoutInfo = Enumerable.Range(3000, 6).ToArray();
        var storeItem = Enumerable.Range(4000, 224).ToArray();
        var petBag = Enumerable.Range(5000, 20).ToArray();
        var friend = Enumerable.Range(0, 10).Select(i => $"Friend{i}").ToArray();
        var costume = Enumerable.Range(6000, 10).ToArray();

        var packet = new AvatarRosterResponse
        {
            VisibleState = 1,
            SpecialState = 2,
            CostumeIndex = 3,
            Tribe = 4,
            PreviousTribe = 5,
            EatLifePotion = 6,
            Gender = 7,
            HeadType = 8,
            FaceType = 9,
            EatStrPotion = 10,
            Level1 = 11,
            Inventory = inventory,
            Level2 = 12,
            EatManaPotion = 13,
            Halo = 14,
            RebirthNum = 15,
            KillOtherTribe = 16,
            SkillPoint = 17,
            Equip = equip,
            EatDexPotion = 18,
            Name = "PlayerOne",
            EatElePotion = 19,
            LogoutInfo = logoutInfo,
            GuildName = "GuildX",
            StoreItem = storeItem,
            PetBag = petBag,
            Friend = friend,
            Teacher = "TeacherA",
            Student = "StudentB",
            Costume = costume
        };

        var actual = new byte[AvatarRosterResponse.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(AvatarRosterResponse.PayloadSize, written);

        var expected = new byte[AvatarRosterResponse.PayloadSize];

        WriteGoldenInt(expected, 0, packet.VisibleState);
        WriteGoldenInt(expected, 4, packet.SpecialState);
        WriteGoldenInt(expected, 8, packet.CostumeIndex);
        WriteGoldenInt(expected, 12, packet.Tribe);
        WriteGoldenInt(expected, 16, packet.PreviousTribe);
        WriteGoldenInt(expected, 20, packet.EatLifePotion);
        WriteGoldenInt(expected, 24, packet.Gender);
        WriteGoldenInt(expected, 28, packet.HeadType);
        WriteGoldenInt(expected, 32, packet.FaceType);
        WriteGoldenInt(expected, 36, packet.EatStrPotion);
        WriteGoldenInt(expected, 40, packet.Level1);
        WriteGoldenIntArray(expected, 44, packet.Inventory);
        WriteGoldenInt(expected, 3116, packet.Level2);
        WriteGoldenInt(expected, 3120, packet.EatManaPotion);
        WriteGoldenInt(expected, 3124, packet.Halo);
        WriteGoldenInt(expected, 3128, packet.RebirthNum);
        WriteGoldenInt(expected, 3132, packet.KillOtherTribe);
        WriteGoldenInt(expected, 3136, packet.SkillPoint);
        WriteGoldenIntArray(expected, 3140, packet.Equip);
        WriteGoldenInt(expected, 3348, packet.EatDexPotion);
        WriteGoldenChar(expected, 3352, 13, packet.Name);
        WriteGoldenInt(expected, 3365, packet.EatElePotion);
        WriteGoldenIntArray(expected, 3369, packet.LogoutInfo);
        WriteGoldenChar(expected, 3393, 13, packet.GuildName);
        WriteGoldenIntArray(expected, 3406, packet.StoreItem);
        WriteGoldenIntArray(expected, 4302, packet.PetBag);
        WriteGoldenChar2Rows(expected, 4382, 13, packet.Friend);
        WriteGoldenChar(expected, 4512, 13, packet.Teacher);
        WriteGoldenChar(expected, 4525, 13, packet.Student);
        WriteGoldenIntArray(expected, 4538, packet.Costume);

        Assert.Equal(expected, actual);
    }

    private static void WriteGoldenInt(byte[] buffer, int offset, int value)
    {
        Span<byte> plain = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(plain, value);

        plain[0] ^= GoldenFirstKey;
        for (var i = 1; i < 4; i++)
            plain[i] ^= GoldenSteadyKey;

        plain.CopyTo(buffer.AsSpan(offset, 4));
    }

    private static void WriteGoldenIntArray(byte[] buffer, int offset, int[] values)
    {
        var length = values.Length * 4;
        var plain = new byte[length];
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(plain.AsSpan(i * 4, 4), values[i]);

        if (length > 0)
        {
            plain[0] ^= GoldenFirstKey;
            for (var i = 1; i < length - 2; i++)
                plain[i] ^= GoldenSteadyKey;
            // plain[length - 2] and plain[length - 1] (the trailing two bytes of the final int) are left
            // exactly as written above: scopyAvtXorIntArr's mask loop never reaches them.
        }

        plain.CopyTo(buffer.AsSpan(offset, length));
    }

    private static void WriteGoldenChar(byte[] buffer, int offset, int length, string value)
    {
        var plain = new byte[length];
        var count = Math.Min(value.Length, length);
        if (count > 0)
            Encoding.Latin1.GetBytes(value, 0, count, plain, 0);

        if (length > 0)
        {
            plain[0] ^= GoldenFirstKey;
            for (var i = 1; i < length - 2; i++)
                plain[i] ^= GoldenSteadyKey;
            // plain[length - 2] stays raw/unmasked (same tail rule as WriteGoldenIntArray)...
            plain[length - 1] = 0; // ...but scopyAvtXorChar additionally forces the final byte to 0.
        }

        plain.CopyTo(buffer.AsSpan(offset, length));
    }

    private static void WriteGoldenChar2Rows(byte[] buffer, int offset, int rowLength, string[] values)
    {
        for (var i = 0; i < values.Length; i++)
            WriteGoldenChar(buffer, offset + i * rowLength, rowLength, values[i]);
    }

    private static int ReadXoredInt(byte[] buffer, int offset)
    {
        var bytes = buffer.AsSpan(offset, 4).ToArray();
        WireXor.XorInt(bytes);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static int[] ReadXoredIntArray(byte[] buffer, int offset, int count)
    {
        var bytes = buffer.AsSpan(offset, count * 4).ToArray();
        WireXor.XorIntArray(bytes);

        var result = new int[count];
        for (var i = 0; i < count; i++)
            result[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i * 4, 4));

        return result;
    }

    private static string ReadXoredChar(byte[] buffer, int offset, int length)
    {
        var bytes = buffer.AsSpan(offset, length).ToArray();
        WireXor.XorIntArray(bytes);

        var nullIndex = Array.IndexOf(bytes, (byte)0);
        return Encoding.Latin1.GetString(bytes, 0, nullIndex < 0 ? bytes.Length : nullIndex);
    }

    private static string[] ReadXoredChar2Rows(byte[] buffer, int offset, int rows, int rowLength)
    {
        var result = new string[rows];
        for (var i = 0; i < rows; i++)
            result[i] = ReadXoredChar(buffer, offset + i * rowLength, rowLength);

        return result;
    }
}
