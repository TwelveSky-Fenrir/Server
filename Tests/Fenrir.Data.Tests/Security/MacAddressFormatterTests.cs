using Fenrir.Data.Security;

namespace Fenrir.Data.Tests.Security;

public class MacAddressFormatterTests
{
    [Fact]
    public void Format_SixByteAddress_RendersUppercaseHyphenatedHex()
    {
        byte[] address = [0x00, 0x1A, 0x2b, 0xFF, 0x0a, 0x09, 0x00, 0x00];

        var result = MacAddressFormatter.Format(address, 6);

        Assert.Equal("00-1A-2B-FF-0A-09", result);
    }

    [Fact]
    public void Format_ZeroLength_ReturnsEmptyString()
    {
        byte[] address = new byte[8];

        var result = MacAddressFormatter.Format(address, 0);

        Assert.Equal("", result);
    }

    [Fact]
    public void Format_LengthLongerThanBuffer_ClampsToBufferLength()
    {
        byte[] address = [0x01, 0x02];

        var result = MacAddressFormatter.Format(address, 8);

        Assert.Equal("01-02", result);
    }
}
