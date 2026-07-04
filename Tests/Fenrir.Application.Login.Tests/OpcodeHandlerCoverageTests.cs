using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Fenrir.Application.Login.Dispatching;
using Fenrir.Contracts;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Application.Login.Tests;

// Closes the gap where a missing handler was only discoverable via LoginFrameDispatcher's runtime
// "No handler registered" warning on first packet receipt.
public class OpcodeHandlerCoverageTests
{
    private static readonly byte[] LegacyNoOpAllowList = [];

    [Fact]
    public void EveryIncomingLoginOpcodeHasARegisteredHandler()
    {
        var declared = DeclaredIncomingOpcodes(typeof(Opcodes).Assembly, FenrirServer.Login);
        var handled = HandledIncomingOpcodes(typeof(LoginFrameDispatcher).Assembly, FenrirServer.Login);

        var missing = declared.Except(handled).Except(LegacyNoOpAllowList).OrderBy(opcode => opcode).ToArray();

        Assert.True(missing.Length == 0,
            $"Login incoming opcode(s) with no registered handler: {string.Join(", ", missing)}");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification =
            "Test-only reflection over Fenrir.Contracts packets; this assembly is never trimmed/AOT-published.")]
    private static HashSet<byte> DeclaredIncomingOpcodes(Assembly contractsAssembly, FenrirServer server)
    {
        return contractsAssembly.GetTypes()
            .Select(t => t.GetCustomAttribute<FenrirPacketAttribute>())
            .Where(a => a is not null && a.Server == server && a.Direction == FenrirDirection.Incoming)
            .Select(a => a!.Opcode)
            .ToHashSet();
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification =
            "Test-only reflection over the generated packet handlers; this assembly is never trimmed/AOT-published.")]
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification =
            "Test-only reflection over the generated packet handlers; this assembly is never trimmed/AOT-published.")]
    private static HashSet<byte> HandledIncomingOpcodes(Assembly applicationAssembly, FenrirServer server)
    {
        return applicationAssembly.GetTypes()
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IInlinePacketHandler<>) ||
                                            i.GetGenericTypeDefinition() == typeof(IAsyncPacketHandler<>)))
            .Select(i => i.GetGenericArguments()[0].GetCustomAttribute<FenrirPacketAttribute>())
            .Where(a => a is not null && a.Server == server && a.Direction == FenrirDirection.Incoming)
            .Select(a => a!.Opcode)
            .ToHashSet();
    }
}
