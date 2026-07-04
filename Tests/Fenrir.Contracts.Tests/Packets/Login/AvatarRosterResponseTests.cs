using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcUserAvatarRecv2Tests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=4579 (1-byte outbound header) -> 4578-byte payload.
        Assert.Equal(4578, AvatarRosterResponse.PayloadSize);
    }

    // Per-field obfuscation (scopyAvtXor*, §3.2): each Write() field comes out XORed with the
    // primitive named by [AvatarXorKind] on that property. Unlike USE_XOR_UID, none of these
    // primitives depend on buffer content, so re-applying them recovers the plaintext.
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
