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
            _ => "Center"
        };
    }

    public static string OpcodeRegistry(FenrirServer server) => PrefixOf(server) + "OpcodeRegistry";

    public static string SessionStateGate(FenrirServer server) => PrefixOf(server) + "SessionStateGate";

    public static string MessageDispatcher(FenrirServer server) => PrefixOf(server) + "MessageDispatcher";

    public static string PacketHandlerHub(FenrirServer server) => PrefixOf(server) + "PacketHandlerHub";

    public static string HandlerRegistration(FenrirServer server) => PrefixOf(server) + "HandlerRegistration";

    public static string AddHandlersMethod(FenrirServer server) => "Add" + PrefixOf(server) + "PacketHandlers";

        public static string NamespaceFor(string? assemblyName)
    {
        return string.IsNullOrWhiteSpace(assemblyName) ? "Fenrir.Generated" : assemblyName!;
    }
}
