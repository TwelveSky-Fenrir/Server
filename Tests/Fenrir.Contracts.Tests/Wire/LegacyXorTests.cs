using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Tests.Wire;

public class LegacyXorTests
{
    // §11: uUserIdx=1000000042 -> tID="MG1000000042" (12 bytes) after XOR_PACKET.
    // Keystream is {0x10,0xFE,0xFE,...} — NOT "+1 with wrap" (a historical misreading).
    [Fact]
    public void ApplyPacketXor_UsesConstantKeystream_MatchesLoginIdWorkedExample()
    {
        var buffer = "MG1000000042"u8.ToArray();

        LegacyXor.ApplyPacketXor(buffer);

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

        LegacyXor.ApplyPacketXor(buffer);

        Assert.Equal(0x10, buffer[0]);
        Assert.All(buffer[1..^1], b => Assert.Equal(0xFE, b));
        Assert.Equal(0xAA, buffer[^1]);
    }

    [Fact]
    public void ApplyPacketXor_BufferOfOneByteOrLess_IsNoOp()
    {
        byte[] singleByte = [0x42];
        LegacyXor.ApplyPacketXor(singleByte);
        Assert.Equal(0x42, singleByte[0]);

        byte[] empty = [];
        LegacyXor.ApplyPacketXor(empty);
        Assert.Empty(empty);
    }

    // XOR_PACKET resets its state on every call, so two consecutive passes on the same buffer
    // restore the original — relied on elsewhere since client and server apply it symmetrically.
    [Fact]
    public void ApplyPacketXor_AppliedTwice_IsInvolution()
    {
        byte[] original = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A];
        var buffer = (byte[])original.Clone();

        LegacyXor.ApplyPacketXor(buffer);
        Assert.NotEqual(original, buffer);

        LegacyXor.ApplyPacketXor(buffer);
        Assert.Equal(original, buffer);
    }

    // USE_XOR_UID: length = position of first null byte, capped to field size.
    // tID="MG12" in a zero-padded char[16] -> length 4; the rest (padding) is untouched.
    [Fact]
    public void ApplyUidXor_StopsAtFirstNullByte_LeavesRemainderOfFieldUntouched()
    {
        byte[] fixedField =
        [
            0x4D, 0x47, 0x31, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];

        LegacyXor.ApplyUidXor(fixedField);

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

        LegacyXor.ApplyUidXor(fixedField);

        byte[] expected = [0x51, 0xBC, 0xBD, 0x44];
        Assert.Equal(expected, fixedField);
    }

    [Fact]
    public void XorInt_XorsAllFourBytes_NoByteSpared()
    {
        byte[] four = [0x11, 0x22, 0x33, 0x44];

        LegacyXor.XorInt(four);

        byte[] expected = [0x01, 0xDC, 0xCD, 0xBA];
        Assert.Equal(expected, four);
    }

    [Fact]
    public void XorIntArray_LeavesLastTwoBytesUnchanged()
    {
        byte[] buffer = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66];

        LegacyXor.XorIntArray(buffer);

        byte[] expected = [0x01, 0xDC, 0xCD, 0xBA, 0x55, 0x66];
        Assert.Equal(expected, buffer);
    }

    [Fact]
    public void XorIntArray_EmptyBuffer_IsNoOp()
    {
        byte[] buffer = [];

        LegacyXor.XorIntArray(buffer);

        Assert.Empty(buffer);
    }

    [Fact]
    public void XorChar_ForcesTerminatorToZero_RegardlessOfOriginalLastByte()
    {
        byte[] buffer = [0x41, 0x42, 0x43, 0x44, 0x99];

        LegacyXor.XorChar(buffer);

        byte[] expected = [0x51, 0xBC, 0xBD, 0x44, 0x00];
        Assert.Equal(expected, buffer);
    }

    // Each row resets its own state (like scopyAvtXorChar2 in C): row 2 must match XorChar
    // being called on it alone.
    [Fact]
    public void XorChar2Rows_AppliesXorCharIndependentlyPerRow()
    {
        byte[] buffer =
        [
            0x41, 0x42, 0x43, 0x44, 0x99,
            0x11, 0x22, 0x33, 0x44, 0x77
        ];

        LegacyXor.XorChar2Rows(buffer, 5);

        byte[] expected =
        [
            0x51, 0xBC, 0xBD, 0x44, 0x00,
            0x01, 0xDC, 0xCD, 0x44, 0x00
        ];
        Assert.Equal(expected, buffer);
    }
}
