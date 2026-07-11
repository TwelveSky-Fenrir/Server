namespace Fenrir.Application.Game.Stats;

/// <summary>
///     Decodes the packed per-socket rune stat (legacy <c>aRuneSystemStat[i]</c>) into its four independent
///     base-attribute contributions, one signed byte each, exactly reproducing the MyFactor read path
///     (<c>Return{IS,IU,IM,IZ}Value</c>, Server/Header/function.h:317-343). Each of those legacy accessors
///     copies one byte of the 32-bit value out through a <c>signed char</c> (the toolchain's default char
///     signedness on the MSVC/Win32 build), so a stored byte of 128-255 reads back NEGATIVE. The four base
///     recompute functions add the decoded value only when it is strictly &gt; 0
///     (Server/Header/Protocol/MyFactor.cpp:1690,1749,1808,1867), so only stored bytes 1-127 ever contribute:
///     a byte of 0 and a byte of 128-255 both add nothing.
///     <para>
///         Byte-to-attribute mapping is fixed: byte 0 -&gt; base Strength (<c>ReturnISValue</c>), byte 1 -&gt;
///         base Dexterity/Wisdom (<c>ReturnIUValue</c>), byte 2 -&gt; base Vitality (<c>ReturnIMValue</c>),
///         byte 3 -&gt; base Intelligence/Ki (<c>ReturnIZValue</c>). A single rune socket can therefore
///         contribute to all four base attributes at once.
///     </para>
/// </summary>
public static class RuneStatDecoder
{
    /// <summary>
    ///     aRuneSystem[4]/aRuneSystemStat[4] -- the fixed per-character rune-socket count
    ///     (Server/Header/Protocol/STRUCT.h:572-573).
    /// </summary>
    public const int SocketCount = 4;

    /// <summary>byte 0 -&gt; base Strength (<c>ReturnISValue</c>, function.h:317-322).</summary>
    public static int Strength(int packed)
    {
        return (sbyte)packed;
    }

    /// <summary>byte 1 -&gt; base Dexterity/Wisdom (<c>ReturnIUValue</c>, function.h:324-329).</summary>
    public static int Dexterity(int packed)
    {
        return (sbyte)(packed >> 8);
    }

    /// <summary>byte 2 -&gt; base Vitality (<c>ReturnIMValue</c>, function.h:331-336).</summary>
    public static int Vitality(int packed)
    {
        return (sbyte)(packed >> 16);
    }

    /// <summary>byte 3 -&gt; base Intelligence/Ki (<c>ReturnIZValue</c>, function.h:338-343).</summary>
    public static int Ki(int packed)
    {
        return (sbyte)(packed >> 24);
    }
}
