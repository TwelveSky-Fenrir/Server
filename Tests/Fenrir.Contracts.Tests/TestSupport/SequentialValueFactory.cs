namespace Fenrir.Contracts.Tests.TestSupport;

/// <summary>
///     Produit des valeurs distinctes et déterministes (index croissant) pour peupler des structs à
///     grand nombre de champs (ex. AvatarInfo, 227 propriétés) sans avoir à inventer une valeur par
///     champ à la main : un bug d'offset qui lit/écrit la mauvaise zone reste détectable puisque deux
///     champs voisins n'ont jamais la même valeur.
/// </summary>
internal sealed class SequentialValueFactory
{
    private int _counter;

    public int NextInt()
    {
        return ++_counter;
    }

    public uint NextUInt()
    {
        return (uint)NextInt();
    }

    public long NextLong()
    {
        return NextInt() * 1_000_000_007L;
    }

    public float NextFloat()
    {
        return NextInt() + 0.25f;
    }

    public byte NextByte()
    {
        return (byte)(NextInt() & 0xFF);
    }

    /// <summary>
    ///     Chaîne courte et unique, tronquée à <paramref name="fixedLength" /> - 1 pour garantir au
    ///     moins un octet de bourrage à zéro (donc un `\0` terminal) même si le compteur grossit.
    /// </summary>
    public string NextString(int fixedLength)
    {
        var value = "S" + NextInt();
        var maxLength = fixedLength - 1;
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    public int[] NextIntArray(int count)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++)
            values[i] = NextInt();
        return values;
    }

    public float[] NextFloatArray(int count)
    {
        var values = new float[count];
        for (var i = 0; i < count; i++)
            values[i] = NextFloat();
        return values;
    }

    public byte[] NextByteArray(int count)
    {
        var values = new byte[count];
        for (var i = 0; i < count; i++)
            values[i] = NextByte();
        return values;
    }

    public string[] NextStringArray(int count, int rowLength)
    {
        var values = new string[count];
        for (var i = 0; i < count; i++)
            values[i] = NextString(rowLength);
        return values;
    }
}
