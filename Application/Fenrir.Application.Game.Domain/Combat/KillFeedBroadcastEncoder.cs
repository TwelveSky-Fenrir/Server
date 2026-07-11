using System.Buffers.Binary;
using System.Text;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Packs a <see cref="KillFeedBroadcastPayload" /> into the 130-byte opaque <c>Data</c> blob the
///     generic, already-existing <c>ZoneEventInfoResponse</c> packet (Sort=3000 for this behavior) carries --
///     the same "opaque payload keyed by Sort" wire family <c>ZoneWar.ZoneEventBroadcaster</c> already uses
///     for its own RvR sort codes (38/39/40/42/45/46/47/1234/1501-1507/etc.), reused here rather than a new
///     dedicated wire packet.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/Header/Protocol/DEFINE.h:69,377-381 (<c>USE_ITEM_LINK_V2</c> unconditional -&gt;
///         the shipped fixed broadcast buffer is 130 bytes, <see cref="PayloadSize" />) ;
///         Server/Header/Protocol/DEFINE.h:280 (avatar name length is 13, <see cref="NameFieldSize" />) ;
///         Server/ts25zone/S07_MyGame02.cpp:12-44 (the leaderboard-entry/feed-payload structures this encoder
///         is transcribing field-for-field) ; Server/ts25zone/S05_MyTransfer.cpp:1131-1136 (the serializer
///         always transmits the full fixed 130-byte payload, tail bytes included).
///     </para>
///     <para>
///         <b>
///             Byte-offset layout is a documented ENGINEERING PLACEHOLDER, not a byte-exact-verified
///             transcription.
///         </b>
///         The source behavior contract's own Open Question 4 states plainly that "the
///         Fenrir wire implementer should confirm the byte-exact expected layout against the legacy client /
///         protocol docs before finalizing the code-3000 payload" -- the contract's citations establish WHICH
///         fields exist (killer/victim name+tribe, three ranked name/tribe/kills triples) and their sizes
///         (13-char names, per <c>DEFINE.h:280</c>) but not their exact byte offsets within the 130-byte
///         blob. This class picks one self-consistent, documented offset scheme (killer name/tribe, victim
///         name/tribe, then three 18-byte name/tribe/kills triples, see the field order below) so the feature
///         is functional end-to-end; every consumer of this class must NOT treat these specific offsets as
///         legacy-verified until a wire-protocol specialist confirms them against the actual legacy client.
///     </para>
///     <para>
///         <b>Non-null-terminated 13-char names.</b> A name exactly 13 bytes long is written with no
///         terminator, matching the source contract's own Edge case ("A maximally-long name can therefore be
///         transmitted without a terminator") -- <see cref="WriteFixedName" /> zero-fills the destination
///         first (so a SHORTER name is still effectively null-padded) and simply does not reserve a
///         terminator slot for the exactly-13-byte case.
///     </para>
/// </remarks>
public static class KillFeedBroadcastEncoder
{
    /// <summary>The fixed <c>ZoneEventInfoResponse.Data</c> size (item-link-v2 shipped build).</summary>
    public const int PayloadSize = 130;

    /// <summary>Avatar name length, Server/Header/Protocol/DEFINE.h:280.</summary>
    public const int NameFieldSize = 13;

    /// <summary>One ranked-entry triple: name (13) + tribe (1) + kills (4, little-endian int32).</summary>
    private const int RankedEntrySize = NameFieldSize + 1 + 4;

    private const int KillerNameOffset = 0;
    private const int KillerTribeOffset = KillerNameOffset + NameFieldSize;
    private const int VictimNameOffset = KillerTribeOffset + 1;
    private const int VictimTribeOffset = VictimNameOffset + NameFieldSize;
    private const int TopThreeOffset = VictimTribeOffset + 1;

    public static byte[] Encode(in KillFeedBroadcastPayload payload)
    {
        var data = new byte[PayloadSize];
        var span = data.AsSpan();

        WriteFixedName(span.Slice(KillerNameOffset, NameFieldSize), payload.KillerName);
        span[KillerTribeOffset] = payload.KillerTribe;

        WriteFixedName(span.Slice(VictimNameOffset, NameFieldSize), payload.VictimName);
        span[VictimTribeOffset] = payload.VictimTribe;

        for (var i = 0; i < 3; i++)
        {
            var entry = i < payload.TopThree.Length ? payload.TopThree[i] : KillFeedTopEntry.Blank;
            var entryOffset = TopThreeOffset + i * RankedEntrySize;

            WriteFixedName(span.Slice(entryOffset, NameFieldSize), entry.Name);
            span[entryOffset + NameFieldSize] = entry.Tribe;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(entryOffset + NameFieldSize + 1, 4), entry.Kills);
        }

        // Bytes from TopThreeOffset + 3*RankedEntrySize (82) through PayloadSize (130) are left zero-filled --
        // the source contract's own "Trailing/uninitialized payload content" edge case: the legacy struct
        // itself only uses 121 of the 130 transmitted bytes.
        return data;
    }

    private static void WriteFixedName(Span<byte> destination, string name)
    {
        destination.Clear();
        var sourceLength = Math.Min(name.Length, destination.Length);
        Encoding.ASCII.GetBytes(name.AsSpan(0, sourceLength), destination);
    }
}
