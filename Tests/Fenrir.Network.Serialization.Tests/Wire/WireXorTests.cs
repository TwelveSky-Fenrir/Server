using Fenrir.Network.Compression;

namespace Fenrir.Network.Serialization.Tests.Wire;

public class WireXorTests
{
    // Keystream is {0x10,0xFE,0xFE,...}, not "+1 with wrap" as sometimes assumed.
    [Fact]
    public void ApplyPacketXor_UsesConstantKeystream_MatchesLoginIdWorkedExample()
    {
        var buffer = "MG1000000042"u8.ToArray();

        WireXor.ApplyPacketXor(buffer);

        byte[] expected =
        [
            0x5D, 0xB9, 0xCF, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE, 0xCA, 0x32
        ];
        Assert.Equal(expected, buffer);
    }

    [Fact]
    public void ApplyPacketXor_LastByteIsNeverModified()
    {
        byte[] buffer = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xAA];

        WireXor.ApplyPacketXor(buffer);

        Assert.Equal(0x10, buffer[0]);
        Assert.All(buffer[1..^1], b => Assert.Equal(0xFE, b));
        Assert.Equal(0xAA, buffer[^1]);
    }

    [Fact]
    public void ApplyPacketXor_BufferOfOneByteOrLess_IsNoOp()
    {
        byte[] singleByte = [0x42];
        WireXor.ApplyPacketXor(singleByte);
        Assert.Equal(0x42, singleByte[0]);

        byte[] empty = [];
        WireXor.ApplyPacketXor(empty);
        Assert.Empty(empty);
    }

    // XOR_PACKET resets state each call, so two passes restore the original (client/server apply it symmetrically).
    [Fact]
    public void ApplyPacketXor_AppliedTwice_IsInvolution()
    {
        byte[] original = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A];
        var buffer = (byte[])original.Clone();

        WireXor.ApplyPacketXor(buffer);
        Assert.NotEqual(original, buffer);

        WireXor.ApplyPacketXor(buffer);
        Assert.Equal(original, buffer);
    }

    // USE_XOR_UID: length = first null byte position (capped to field size); the rest is left untouched.
    [Fact]
    public void ApplyUidXor_StopsAtFirstNullByte_LeavesRemainderOfFieldUntouched()
    {
        byte[] fixedField =
        [
            0x4D, 0x47, 0x31, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];

        WireXor.ApplyUidXor(fixedField);

        byte[] expected =
        [
            0x5D, 0xB9, 0xCF, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];
        Assert.Equal(expected, fixedField);
    }

    [Fact]
    public void ApplyUidXor_NoNullByteFound_UsesEntireFieldAsLength()
    {
        byte[] fixedField = [0x41, 0x42, 0x43, 0x44];

        WireXor.ApplyUidXor(fixedField);

        byte[] expected = [0x51, 0xBC, 0xBD, 0x44];
        Assert.Equal(expected, fixedField);
    }

    [Fact]
    public void XorInt_XorsAllFourBytes_NoByteSpared()
    {
        byte[] four = [0x11, 0x22, 0x33, 0x44];

        WireXor.XorInt(four);

        byte[] expected = [0x01, 0xDC, 0xCD, 0xBA];
        Assert.Equal(expected, four);
    }

    [Fact]
    public void XorIntArray_LeavesLastTwoBytesUnchanged()
    {
        byte[] buffer = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66];

        WireXor.XorIntArray(buffer);

        byte[] expected = [0x01, 0xDC, 0xCD, 0xBA, 0x55, 0x66];
        Assert.Equal(expected, buffer);
    }

    [Fact]
    public void XorIntArray_EmptyBuffer_IsNoOp()
    {
        byte[] buffer = [];

        WireXor.XorIntArray(buffer);

        Assert.Empty(buffer);
    }

    [Fact]
    public void XorChar_ForcesTerminatorToZero_RegardlessOfOriginalLastByte()
    {
        byte[] buffer = [0x41, 0x42, 0x43, 0x44, 0x99];

        WireXor.XorChar(buffer);

        byte[] expected = [0x51, 0xBC, 0xBD, 0x44, 0x00];
        Assert.Equal(expected, buffer);
    }

    // Each row resets its own state (like scopyAvtXorChar2 in C), independent of other rows.
    [Fact]
    public void XorChar2Rows_AppliesXorCharIndependentlyPerRow()
    {
        byte[] buffer =
        [
            0x41, 0x42, 0x43, 0x44, 0x99,
            0x11, 0x22, 0x33, 0x44, 0x77
        ];

        WireXor.XorChar2Rows(buffer, 5);

        byte[] expected =
        [
            0x51, 0xBC, 0xBD, 0x44, 0x00,
            0x01, 0xDC, 0xCD, 0x44, 0x00
        ];
        Assert.Equal(expected, buffer);
    }
}
