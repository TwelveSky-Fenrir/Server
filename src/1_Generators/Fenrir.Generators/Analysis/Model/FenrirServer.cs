namespace Fenrir.Generators.Analysis.Model;

internal enum FenrirServer : byte
{
    Login,
    Zone,

    // Ordinals MUST stay aligned with the runtime enum Fenrir.Core.Wire.FenrirServer (Login=0, Zone=1,
    // Center=2). Center = Fenrir addition: the S2S CenterServer, whose incoming frames carry only the
    // 1-byte opcode header (ex-SV_DEFAULT_PACKET), never the 9-byte client header.
    Center
}
