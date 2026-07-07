using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

/// <summary>Discovered <c>IInlinePacketHandler&lt;T&gt;</c>/<c>IAsyncPacketHandler&lt;T&gt;</c> handler.</summary>
/// <remarks>
///     A <c>readonly record struct</c> (not <c>class</c>): every member is a simple value/string/
///     <see cref="Location" />, fully immutable after construction, and small enough that boxing-free
///     structural equality is a strict win for <c>HandlerDispatchIncrementalGenerator</c>'s
///     <c>Collect()</c>/<c>Deduplicate</c>/<c>HandlerCollisionChecker</c> pipeline. It never carried an
///     <see cref="Microsoft.CodeAnalysis.ISymbol" /> in the first place (dispatch only ever reads
///     <c>Server</c>/<c>Opcode</c> straight off the packet's attribute data, plus the handler type's own
///     display-string names), so unlike <see cref="TypeModel" />/<see cref="FieldModel" /> there was nothing
///     to delete here. <see cref="Location" /> carries the same caching caveat noted on
///     <see cref="TypeModel.Location" /> (a syntax tree's identity changes on every edit) but is likewise kept
///     because it's genuinely read downstream, by <c>HandlerCollisionChecker</c> to anchor the <c>FEN015</c>
///     diagnostic.
/// </remarks>
internal readonly record struct HandlerModel
{
    public required string HandlerTypeFullName { get; init; }

    public required string PacketTypeFullName { get; init; }

    public required FenrirServer Server { get; init; }

    public required byte Opcode { get; init; }

    public required bool IsAsync { get; init; }

    public required Location Location { get; init; }
}
