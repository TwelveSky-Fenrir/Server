using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Fenrir.Network.Serialization.Tests.TestSupport;

internal static class StructuralAssert
{
    public static void DeepEqual<T>(T expected, T actual)
        where T : struct
    {
        Compare(typeof(T), expected, actual);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification =
            "Test-only reflection over Fenrir.Contracts DTOs; this assembly is never trimmed/AOT-published.")]
    private static void Compare(Type type, object? expected, object? actual)
    {
        switch (expected)
        {
            case int[] expectedInts:
                Assert.Equal(expectedInts, (int[])actual!);
                return;
            case float[] expectedFloats:
                Assert.Equal(expectedFloats, (float[])actual!);
                return;
            case byte[] expectedBytes:
                Assert.Equal(expectedBytes, (byte[])actual!);
                return;
            case string[] expectedStrings:
                Assert.Equal(expectedStrings, (string[])actual!);
                return;
            case Array expectedArray:
            {
                var actualArray = (Array)actual!;
                Assert.Equal(expectedArray.Length, actualArray.Length);
                var elementType = expectedArray.GetType().GetElementType()!;
                for (var i = 0; i < expectedArray.Length; i++)
                    Compare(elementType, expectedArray.GetValue(i), actualArray.GetValue(i));
                return;
            }
        }

        if (type is { Namespace: "Fenrir.Network.Serialization.Shared.Packets.Shared", IsValueType: true })
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Compare(property.PropertyType, property.GetValue(expected), property.GetValue(actual));
            return;
        }

        Assert.Equal(expected, actual);
    }
}
