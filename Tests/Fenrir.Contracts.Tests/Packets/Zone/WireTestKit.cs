using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     Shared Zone packet test helpers: reflection-based population of wire structs (avoids hand-copying
///     the ~190 properties of AVATAR_INFO/WORLD_INFO/TRIBE_INFO in every test), deep field-by-field
///     comparison, and a manual "golden" encoder for ACTION_INFO/OBJECT_FOR_AVATAR independent of the generator.
/// </summary>
internal static class WireTestKit
{
    public static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }

    /// <summary>
    ///     Manual decoder mirroring <see cref="FixedStringAttribute" />'s contract (null-terminated,
    ///     Latin1) — used by golden-bytes tests to read back a fixed-width <c>char[N]</c> field from a
    ///     hand-built buffer without depending on the generated <c>TryRead</c>.
    /// </summary>
    public static string ReadFixedString(ReadOnlySpan<byte> source)
    {
        var nul = source.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nul >= 0 ? source[..nul] : source);
    }

    public static T CreatePopulated<T>(int seed = 0) where T : struct
    {
        object boxed = default(T);
        PopulateProperties(typeof(T), boxed, ref seed);
        return (T)boxed;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification =
            "Test-only reflection over Fenrir.Contracts DTOs; this assembly is never trimmed/AOT-published.")]
    private static void PopulateProperties(Type type, object instance, ref int seed)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
                continue;

            property.SetValue(instance, CreateValue(property, ref seed));
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification =
            "Test-only reflection over Fenrir.Contracts DTOs; this assembly is never trimmed/AOT-published.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification =
            "Test-only Array.CreateInstance over Fenrir.Contracts DTOs; this assembly is never trimmed/AOT-published.")]
    private static object CreateValue(PropertyInfo property, ref int seed)
    {
        var type = property.PropertyType;

        if (type == typeof(int))
            return unchecked(++seed);
        if (type == typeof(uint))
            return unchecked((uint)(++seed));
        if (type == typeof(float))
            return ++seed + 0.25f;
        if (type == typeof(long))
            return unchecked(++seed * 1_000_000_007L);
        if (type == typeof(byte))
            return unchecked((byte)(++seed));

        if (type == typeof(string))
            return CreateFixedString(property.GetCustomAttribute<FixedStringAttribute>()!.Length, ref seed);

        if (type == typeof(int[]))
        {
            var count = property.GetCustomAttribute<FixedArrayAttribute>()!.ElementCount;
            var values = new int[count];
            for (var i = 0; i < count; i++)
                values[i] = unchecked(++seed);
            return values;
        }

        if (type == typeof(float[]))
        {
            var count = property.GetCustomAttribute<FixedArrayAttribute>()!.ElementCount;
            var values = new float[count];
            for (var i = 0; i < count; i++)
                values[i] = ++seed + 0.5f;
            return values;
        }

        if (type == typeof(byte[]))
        {
            var count = property.GetCustomAttribute<FixedArrayAttribute>()!.ElementCount;
            var values = new byte[count];
            for (var i = 0; i < count; i++)
                values[i] = unchecked((byte)(++seed));
            return values;
        }

        if (type == typeof(string[]))
        {
            var rows = property.GetCustomAttribute<FixedArrayAttribute>()!.ElementCount;
            var values = new string[rows];
            for (var i = 0; i < rows; i++)
                values[i] = CreateFixedString(13, ref seed);
            return values;
        }

        // Arrays of nested wire structs (FieldShape.NestedArray): populate each element recursively.
        if (type.IsArray && type.GetElementType() is { IsValueType: true, IsPrimitive: false } structElementType)
        {
            var count = property.GetCustomAttribute<FixedArrayAttribute>()!.ElementCount;
            var values = Array.CreateInstance(structElementType, count);
            for (var i = 0; i < count; i++)
            {
                var element = Activator.CreateInstance(structElementType)!;
                PopulateProperties(structElementType, element, ref seed);
                values.SetValue(element, i);
            }

            return values;
        }

        if (type.IsValueType)
        {
            var nested = Activator.CreateInstance(type)!;
            PopulateProperties(type, nested, ref seed);
            return nested;
        }

        throw new NotSupportedException(
            $"{property.DeclaringType}.{property.Name}: type {type} not supported by WireTestKit.");
    }

    private static string CreateFixedString(int length, ref int seed)
    {
        var visible = Math.Max(0, length - 1);
        var chars = new char[visible];
        for (var i = 0; i < visible; i++)
            chars[i] = (char)('A' + ++seed % 26);
        return new string(chars);
    }

    public static void AssertDeepEqual<T>(T expected, T actual)
    {
        AssertDeepEqual(typeof(T), expected, actual, typeof(T).Name);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification =
            "Test-only reflection over Fenrir.Contracts DTOs; this assembly is never trimmed/AOT-published.")]
    private static void AssertDeepEqual(Type type, object? expected, object? actual, string path)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
                continue;

            var expectedValue = property.GetValue(expected);
            var actualValue = property.GetValue(actual);
            var propertyPath = $"{path}.{property.Name}";

            switch (expectedValue)
            {
                case int[] a:
                    Assert.True(a.SequenceEqual((int[])actualValue!), propertyPath);
                    break;
                case float[] a:
                    Assert.True(a.SequenceEqual((float[])actualValue!), propertyPath);
                    break;
                case byte[] a:
                    Assert.True(a.SequenceEqual((byte[])actualValue!), propertyPath);
                    break;
                case string[] a:
                    Assert.True(a.SequenceEqual((string[])actualValue!), propertyPath);
                    break;
                // Arrays of nested wire structs (FieldShape.NestedArray): element-wise deep comparison.
                case Array a:
                {
                    var actualArray = (Array)actualValue!;
                    Assert.True(a.Length == actualArray.Length, propertyPath + ".Length");
                    var elementType = a.GetType().GetElementType()!;
                    for (var i = 0; i < a.Length; i++)
                        AssertDeepEqual(elementType, a.GetValue(i), actualArray.GetValue(i), $"{propertyPath}[{i}]");
                    break;
                }
                case string s:
                    Assert.True(s == (string?)actualValue, propertyPath);
                    break;
                default:
                    if (property.PropertyType.IsValueType && !property.PropertyType.IsPrimitive)
                        AssertDeepEqual(property.PropertyType, expectedValue, actualValue, propertyPath);
                    else
                        Assert.True(Equals(expectedValue, actualValue), propertyPath);
                    break;
            }
        }
    }

    /// <summary>
    ///     Manual encoder for ACTION_INFO (104 bytes, no padding) per wire contract §4.2; independent
    ///     of <c>ActionInfo.Write</c> so it can serve as a golden reference.
    /// </summary>
    public static int EncodeActionInfo(Span<byte> destination, ActionInfo action)
    {
        var offset = 0;
        WriteInt(destination, ref offset, action.Type);
        WriteInt(destination, ref offset, action.Sort);
        WriteFloat(destination, ref offset, action.Frame);
        WriteFloats(destination, ref offset, action.Location);
        WriteFloats(destination, ref offset, action.TargetLocation);
        WriteFloat(destination, ref offset, action.Front);
        WriteFloat(destination, ref offset, action.TargetFront);
        WriteFloats(destination, ref offset, action.PetLocation);
        WriteFloats(destination, ref offset, action.PetTargetLocation);
        WriteFloat(destination, ref offset, action.PetFront);
        WriteInt(destination, ref offset, action.PetSort);
        WriteInt(destination, ref offset, action.TargetObjectSort);
        WriteInt(destination, ref offset, action.TargetObjectIndex);
        WriteInt(destination, ref offset, action.TargetObjectUniqueNumber);
        WriteInt(destination, ref offset, action.SkillNumber);
        WriteInt(destination, ref offset, action.SkillGradeNum1);
        WriteInt(destination, ref offset, action.SkillGradeNum2);
        WriteInt(destination, ref offset, action.SkillValue);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for OBJECT_FOR_AVATAR (632 bytes) per wire contract §5.9/§7.3: 5 padding
    ///     zones of 3 bytes each (offsets 29, 41, 61, 489, 533); independent of <c>ObjectForAvatar.Write</c>.
    /// </summary>
    public static int EncodeObjectForAvatar(Span<byte> destination, ObjectForAvatar data)
    {
        var offset = 0;
        WriteInt(destination, ref offset, data.VisibleState);
        WriteInt(destination, ref offset, data.SpecialState);
        WriteInt(destination, ref offset, data.KillOtherTribe);
        WriteInt(destination, ref offset, data.GoodFellow);
        WriteString(destination, ref offset, data.GuildName, 13);
        Pad(destination, ref offset, 3);
        WriteInt(destination, ref offset, data.GuildRole);
        WriteString(destination, ref offset, data.CallName, 5);
        Pad(destination, ref offset, 3);
        WriteInt(destination, ref offset, data.GuildMarkEffect);
        WriteString(destination, ref offset, data.Name, 13);
        Pad(destination, ref offset, 3);
        WriteInt(destination, ref offset, data.Tribe);
        WriteInt(destination, ref offset, data.PreviousTribe);
        WriteInt(destination, ref offset, data.Gender);
        WriteInt(destination, ref offset, data.HeadType);
        WriteInt(destination, ref offset, data.FaceType);
        WriteInt(destination, ref offset, data.Level1);
        WriteInt(destination, ref offset, data.Level2);
        WriteInts(destination, ref offset, data.EquipForView);
        WriteInt(destination, ref offset, data.AnimalNumber);
        WriteInt(destination, ref offset, data.Title);
        WriteInt(destination, ref offset, data.Halo);
        WriteInt(destination, ref offset, data.RebirthNum);
        WriteInt(destination, ref offset, data.BattleTeam);
        offset += EncodeActionInfo(destination[offset..], data.Action);
        WriteInt(destination, ref offset, data.MaxLifeValue);
        WriteInt(destination, ref offset, data.LifeValue);
        WriteInt(destination, ref offset, data.MaxManaValue);
        WriteInt(destination, ref offset, data.ManaValue);
        WriteInts(destination, ref offset, data.EffectValueForView);
        WriteString(destination, ref offset, data.PartyName, 13);
        Pad(destination, ref offset, 3);
        WriteInts(destination, ref offset, data.DuelState);
        WriteInt(destination, ref offset, data.PShopState);
        WriteString(destination, ref offset, data.PShopName, 25);
        Pad(destination, ref offset, 3);
        WriteInt(destination, ref offset, data.CostumeNumber);
        WriteInt(destination, ref offset, data.BufEffectTimeState);
        WriteInt(destination, ref offset, data.BufSort);
        WriteInt(destination, ref offset, data.AutoState);
        WriteInt(destination, ref offset, data.FishingState);
        WriteInt(destination, ref offset, data.FishingStep);
        WriteFloats(destination, ref offset, data.FishingPoint);
        WriteInt(destination, ref offset, data.RankPoint);
        WriteInt(destination, ref offset, data.TargetState);
        WriteInt(destination, ref offset, data.AnimalAbsorbState);
        WriteInt(destination, ref offset, data.PetValid);
        WriteInt(destination, ref offset, data.Unk1);
        WriteFloats(destination, ref offset, data.PetLocation);
        WriteFloat(destination, ref offset, data.PetFrame);
        WriteInt(destination, ref offset, data.Unk624);
        WriteInt(destination, ref offset, data.Unk625);
        WriteInt(destination, ref offset, data.UniqueSkillNumber);
        WriteInt(destination, ref offset, data.UniqueSkillBuffTime);
        WriteInt(destination, ref offset, data.CostumeState);
        WriteInt(destination, ref offset, data.StellarCoreNumber);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for ITEM_LINK_INFO (24 bytes, STRUCT.h:1009-1014) per the chat/notice wire
    ///     contract (§ Lot 02): Index/Activity/Value/Socket[3], no padding; independent of
    ///     <c>ItemLinkInfo.Write</c>.
    /// </summary>
    public static int EncodeItemLinkInfo(Span<byte> destination, ItemLinkInfo link)
    {
        var offset = 0;
        WriteInt(destination, ref offset, link.Index);
        WriteInt(destination, ref offset, link.Activity);
        WriteInt(destination, ref offset, link.Value);
        WriteInts(destination, ref offset, link.Socket);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for OBJECT_FOR_MONSTER (112 bytes, STRUCT.h:933-938) per the replication+combat
    ///     wire contract (§ Lot 01): a leading int, the 104-byte ACTION_INFO block, a trailing int — zero
    ///     padding (every member already 4-byte aligned); independent of <c>ObjectForMonster.Write</c>.
    /// </summary>
    public static int EncodeObjectForMonster(Span<byte> destination, ObjectForMonster data)
    {
        var offset = 0;
        WriteInt(destination, ref offset, data.Index);
        offset += EncodeActionInfo(destination[offset..], data.Action);
        WriteInt(destination, ref offset, data.LifeValue);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for OBJECT_FOR_ITEM (84 bytes, STRUCT.h:941-955) per the replication+combat wire
    ///     contract (§ Lot 01): a 2-byte natural padding zone after <c>PartyName</c> (offset 54) so that
    ///     <c>DropSort</c> lands on a 4-byte boundary (offset 56); independent of <c>ObjectForItem.Write</c>.
    /// </summary>
    public static int EncodeObjectForItem(Span<byte> destination, ObjectForItem data)
    {
        var offset = 0;
        WriteInt(destination, ref offset, data.Index);
        WriteInt(destination, ref offset, data.Quantity);
        WriteInt(destination, ref offset, data.Value);
        WriteInt(destination, ref offset, data.SerialNumber);
        WriteFloats(destination, ref offset, data.Location);
        WriteString(destination, ref offset, data.Master, 13);
        WriteString(destination, ref offset, data.PartyName, 13);
        Pad(destination, ref offset, 2);
        WriteInt(destination, ref offset, data.DropSort);
        WriteUInt(destination, ref offset, data.CreateTime);
        WriteUInt(destination, ref offset, data.PresentTime);
        WriteInt(destination, ref offset, data.CreateState);
        WriteInts(destination, ref offset, data.SocketGem);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for ATTACK_FOR_PROTOCOL (68 bytes, STRUCT.h:958-978) per the replication+combat
    ///     wire contract (§ Lot 01): 17 four-byte fields, no padding (the <c>#ifdef GXCW</c> tail member is
    ///     not compiled in EU33); independent of <c>AttackForProtocol.Write</c>.
    /// </summary>
    public static int EncodeAttackForProtocol(Span<byte> destination, AttackForProtocol data)
    {
        var offset = 0;
        WriteInt(destination, ref offset, data.Case);
        WriteInt(destination, ref offset, data.ServerIndex1);
        WriteUInt(destination, ref offset, data.UniqueNumber1);
        WriteInt(destination, ref offset, data.ServerIndex2);
        WriteUInt(destination, ref offset, data.UniqueNumber2);
        WriteFloats(destination, ref offset, data.SenderLocation);
        WriteInt(destination, ref offset, data.AttackActionValue1);
        WriteInt(destination, ref offset, data.AttackActionValue2);
        WriteInt(destination, ref offset, data.AttackActionValue3);
        WriteInt(destination, ref offset, data.AttackActionValue4);
        WriteInt(destination, ref offset, data.AttackResultValue);
        WriteInt(destination, ref offset, data.AttackCriticalExist);
        WriteInt(destination, ref offset, data.AttackElementDamage);
        WriteInt(destination, ref offset, data.AttackViewDamageValue);
        WriteInt(destination, ref offset, data.AttackRealDamageValue);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for PSHOP_INFO (1232 bytes, STRUCT.h:703-709) per the commerce wire contract
    ///     (§ Lot 04): a 3-byte natural padding zone after <c>Name</c> (char[25], offset 4..28) so the
    ///     following <c>int[5][5][9]</c> array lands on a 4-byte boundary (offset 32); independent of
    ///     <c>PshopInfo.Write</c>.
    /// </summary>
    public static int EncodePshopInfo(Span<byte> destination, PshopInfo data)
    {
        var offset = 0;
        WriteUInt(destination, ref offset, data.UniqueNumber);
        WriteString(destination, ref offset, data.Name, 25);
        Pad(destination, ref offset, 3);
        WriteInts(destination, ref offset, data.ItemInfo);
        WriteInts(destination, ref offset, data.SocketInfo);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for the 50-byte real-field portion of PROXY_STATE_INFO (STRUCT.h:1734-1740) per
    ///     the commerce wire contract (§ Lot 04): Location/Name/PshopName, no internal padding (the C++
    ///     struct's 2 trailing padding bytes are NOT part of this wire type — see
    ///     <see cref="ProxyStateInfo" />'s remarks); independent of <c>ProxyStateInfo.Write</c>.
    /// </summary>
    public static int EncodeProxyStateInfo(Span<byte> destination, ProxyStateInfo data)
    {
        var offset = 0;
        WriteFloats(destination, ref offset, data.Location);
        WriteString(destination, ref offset, data.Name, 13);
        WriteString(destination, ref offset, data.PshopName, 25);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for BLOOD_ITEM (12 bytes, STRUCT.h:1423-1428) per the commerce wire contract
    ///     (§ Lot 04): ItemId/Price/Quantity, no padding; independent of <c>BloodItem.Write</c>.
    /// </summary>
    public static int EncodeBloodItem(Span<byte> destination, BloodItem data)
    {
        var offset = 0;
        WriteInt(destination, ref offset, data.ItemId);
        WriteInt(destination, ref offset, data.Price);
        WriteInt(destination, ref offset, data.Quantity);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for BLOOD_SHOP (604 bytes, STRUCT.h:1429-1433) per the commerce wire contract
    ///     (§ Lot 04): BloodNum followed by 50 nested <see cref="BloodItem" /> slots, no padding;
    ///     independent of <c>BloodShop.Write</c>.
    /// </summary>
    public static int EncodeBloodShop(Span<byte> destination, BloodShop data)
    {
        var offset = 0;
        WriteInt(destination, ref offset, data.BloodNum);
        foreach (var item in data.Data)
            offset += EncodeBloodItem(destination[offset..], item);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for PROXY_SHOP_ITEM (20 bytes, STRUCT.h:1742-1749) per the commerce wire
    ///     contract (§ Lot 04): Id/Quantity/Value/Serial/Price, no padding; independent of
    ///     <c>ProxyShopItem.Write</c>.
    /// </summary>
    public static int EncodeProxyShopItem(Span<byte> destination, ProxyShopItem data)
    {
        var offset = 0;
        WriteInt(destination, ref offset, data.Id);
        WriteInt(destination, ref offset, data.Quantity);
        WriteInt(destination, ref offset, data.Value);
        WriteInt(destination, ref offset, data.Serial);
        WriteInt(destination, ref offset, data.Price);
        return offset;
    }

    /// <summary>
    ///     Manual encoder for PROXY_SHOP_USER_INFO (824 bytes, STRUCT.h:1752-1760) per the commerce wire
    ///     contract (§ Lot 04): a 3-byte natural padding zone after <c>AvatarName</c> (offset 13..15)
    ///     before the 25 nested <see cref="ProxyShopItem" /> slots; independent of
    ///     <c>ProxyShopUserInfo.Write</c>.
    /// </summary>
    public static int EncodeProxyShopUserInfo(Span<byte> destination, ProxyShopUserInfo data)
    {
        var offset = 0;
        WriteString(destination, ref offset, data.AvatarName, 13);
        Pad(destination, ref offset, 3);
        foreach (var item in data.Items)
            offset += EncodeProxyShopItem(destination[offset..], item);
        WriteInts(destination, ref offset, data.Sockets);
        WriteInt(destination, ref offset, data.Money);
        WriteInt(destination, ref offset, data.BigMoney);
        return offset;
    }

    private static void WriteInt(Span<byte> destination, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], value);
        offset += 4;
    }

    private static void WriteUInt(Span<byte> destination, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], value);
        offset += 4;
    }

    private static void WriteFloat(Span<byte> destination, ref int offset, float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], value);
        offset += 4;
    }

    private static void WriteInts(Span<byte> destination, ref int offset, int[] values)
    {
        foreach (var value in values)
            WriteInt(destination, ref offset, value);
    }

    private static void WriteFloats(Span<byte> destination, ref int offset, float[] values)
    {
        foreach (var value in values)
            WriteFloat(destination, ref offset, value);
    }

    private static void WriteString(Span<byte> destination, ref int offset, string value, int length)
    {
        WriteFixedString(destination.Slice(offset, length), value);
        offset += length;
    }

    private static void Pad(Span<byte> destination, ref int offset, int length)
    {
        destination.Slice(offset, length).Clear();
        offset += length;
    }
}
