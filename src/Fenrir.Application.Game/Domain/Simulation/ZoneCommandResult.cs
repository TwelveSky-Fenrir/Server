namespace Fenrir.Application.Game.Domain.Simulation;

public enum ZoneCommandResultKind : byte
{
    Applied = 1,

    Rejected,

    Backpressured,

    Cancelled,

    Faulted
}

public readonly record struct ZoneCommandResult
{
    private ZoneCommandResult(ZoneCommandResultKind kind, string? cause)
    {
        Kind = kind;
        Cause = cause;
    }

    public ZoneCommandResultKind Kind { get; }

    public string? Cause { get; }

    public static ZoneCommandResult Applied() => new(ZoneCommandResultKind.Applied, null);

    public static ZoneCommandResult Rejected(string? cause = null) => new(ZoneCommandResultKind.Rejected, cause);

    public static ZoneCommandResult Backpressured(string? cause = null) =>
        new(ZoneCommandResultKind.Backpressured, cause);

    public static ZoneCommandResult Cancelled(string? cause = null) => new(ZoneCommandResultKind.Cancelled, cause);

    public static ZoneCommandResult Faulted(string? cause = null) => new(ZoneCommandResultKind.Faulted, cause);
}
