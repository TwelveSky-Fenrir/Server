using Fenrir.Generators.Analysis.Model;

namespace Fenrir.Generators.Analysis.Support;

internal static class EmittedNames
{
    public static string PrefixOf(FenrirServer server)
    {
        return server switch
        {
            FenrirServer.Login => "Login",
            FenrirServer.Zone => "Zone",
            _ => throw new System.ArgumentOutOfRangeException(nameof(server), server, null)
        };
    }

    public static string OpcodeRegistry(FenrirServer server)
    {
        return PrefixOf(server) + "OpcodeRegistry";
    }

    public static string SessionStateGate(FenrirServer server)
    {
        return PrefixOf(server) + "SessionStateGate";
    }

    public static string MessageDispatcher(FenrirServer server)
    {
        return PrefixOf(server) + "MessageDispatcher";
    }

    public static string PacketHandlerHub(FenrirServer server)
    {
        return PrefixOf(server) + "PacketHandlerHub";
    }

    public static string HandlerRegistration(FenrirServer server)
    {
        return PrefixOf(server) + "HandlerRegistration";
    }

    public static string AddHandlersMethod(FenrirServer server)
    {
        return "Add" + PrefixOf(server) + "PacketHandlers";
    }

    public static string NamespaceFor(string? assemblyName)
    {
        return string.IsNullOrWhiteSpace(assemblyName) ? "Fenrir.Generated" : assemblyName!;
    }
}
