using System.Buffers;
using System.Text;

namespace Fenrir.Network.Util;

public static partial class Utils
{
    // Source: https://devblogs.microsoft.com/dotnet/system-io-pipelines-high-performance-io-in-net/
    
    // TODO: Consider if this should be made into an extension method?
    
    public static string GetAsciiString(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsSingleSegment)
        {
            return Encoding.ASCII.GetString(buffer.First.Span);
        }

        return string.Create((int)buffer.Length, buffer, (span, sequence) =>
        {
            foreach (var segment in sequence)
            {
                Encoding.ASCII.GetChars(segment.Span, span);
                span = span.Slice(segment.Length);
            }
        });
    }

}