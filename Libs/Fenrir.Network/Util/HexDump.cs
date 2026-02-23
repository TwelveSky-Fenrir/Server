using System.Buffers;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Fenrir.Network.Util;

// Source: https://www.codeproject.com/Articles/36747/Quick-and-Dirty-HexDump-of-a-Byte-Array   

public static partial class Utils
{
    public static bool IsMarshalable(Type type)
    {
        return type.IsValueType && type.StructLayoutAttribute != null && Marshal.SizeOf(type) > 0;
    }

    // TODO: make these work directly on the span/sequence/memory as to not copy.
    private static string HexDump(SequenceReader<byte> sequence, int bytesPerLine = 16)
    {
        return HexDump(sequence.UnreadSpan.ToArray(), bytesPerLine);
    }
    
    public static string HexDump(ReadOnlySequence<byte> sequence, int bytesPerLine = 16)
    {
        return HexDump(sequence.Slice(sequence.Start, sequence.Length).ToArray(), bytesPerLine);
    }
    
    public static string HexDump(Memory<byte> memory, int bytesPerLine = 16)
    {
        return HexDump(memory.Span.ToArray(), bytesPerLine);
    }
    
    public static string HexDump(object obj, int bytesPerLine = 16)
    {
        // If object can be marsheled to a byte array, then we can dump it.
         if (!IsMarshalable(obj.GetType())) return "Unable to HexDump <not marshallable>";

        var size = Marshal.SizeOf(obj);
        var bytes = new byte[size];
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(obj, ptr, true);
        Marshal.Copy(ptr, bytes, 0, size);
        Marshal.FreeHGlobal(ptr);

        return HexDump(bytes, bytesPerLine);
    }
    
    // TODO: Convert to span.
    public static string HexDump(byte[] bytes, int bytesPerLine = 16)
    {
        if (bytes == null) return "<null>";
        var bytesLength = bytes.Length;

        var HexChars = "0123456789ABCDEF".ToCharArray();

        var firstHexColumn =
            8 // 8 characters for the address
            + 3; // 3 spaces

        var firstCharColumn = firstHexColumn
                              + bytesPerLine * 3 // - 2 digit for the hexadecimal value and 1 space
                              + (bytesPerLine - 1) / 8 // - 1 extra space every 8 characters from the 9th
                              + 2; // 2 spaces 

        var lineLength = firstCharColumn
                         + bytesPerLine // - characters to show the ascii value
                         + Environment.NewLine.Length; // Carriage return and line feed (should normally be 2)
        
        var line = (new string(' ', lineLength - Environment.NewLine.Length) + Environment.NewLine).ToCharArray();
        string sizeInfo = $"Size: {bytesLength} ";

        var expectedLines = (bytesLength + bytesPerLine - 1) / bytesPerLine;
        var result = new StringBuilder(sizeInfo.Length + Environment.NewLine.Length + (expectedLines * lineLength));
        result.AppendLine(sizeInfo);

        for (var i = 0; i < bytesLength; i += bytesPerLine)
        {
            line[0] = HexChars[(i >> 28) & 0xF];
            line[1] = HexChars[(i >> 24) & 0xF];
            line[2] = HexChars[(i >> 20) & 0xF];
            line[3] = HexChars[(i >> 16) & 0xF];
            line[4] = HexChars[(i >> 12) & 0xF];
            line[5] = HexChars[(i >> 8) & 0xF];
            line[6] = HexChars[(i >> 4) & 0xF];
            line[7] = HexChars[(i >> 0) & 0xF];

            var hexColumn = firstHexColumn;
            var charColumn = firstCharColumn;

            for (var j = 0; j < bytesPerLine; j++)
            {
                if (j > 0 && (j & 7) == 0) hexColumn++;
                if (i + j >= bytesLength)
                {
                    line[hexColumn] = ' ';
                    line[hexColumn + 1] = ' ';
                    line[charColumn] = ' ';
                }
                else
                {
                    var b = bytes[i + j];
                    line[hexColumn] = HexChars[(b >> 4) & 0xF];
                    line[hexColumn + 1] = HexChars[b & 0xF];
                    // TODO: May not handle UTF-8 correctly.
                    // TODO: May not handle other chars correctly.
                    bool isTypableAscii = b <= 32 && b <= 126;
                    line[charColumn] = isTypableAscii ? '.' : (char)b; // &middot; if html.
                }
                
                // TODO: Colour coding?
                // Make 0 bytes dimmer, make control characters import colour?

                hexColumn += 3;
                charColumn++;
            }

            result.Append(line);
        }

        return result.ToString();
    }
}
