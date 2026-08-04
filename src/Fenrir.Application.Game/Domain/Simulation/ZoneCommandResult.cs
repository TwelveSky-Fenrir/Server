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

    public static ZoneCommandResult Applied()
    {
        return new ZoneCommandResult(ZoneCommandResultKind.Applied, null);
    }

    public static ZoneCommandResult Rejected(string? cause = null)
    {
        return new ZoneCommandResult(ZoneCommandResultKind.Rejected, cause);
    }

    public static ZoneCommandResult Backpressured(string? cause = null)
    {
        return new ZoneCommandResult(ZoneCommandResultKind.Backpressured, cause);
    }

    public static ZoneCommandResult Cancelled(string? cause = null)
    {
        return new ZoneCommandResult(ZoneCommandResultKind.Cancelled, cause);
    }

    public static ZoneCommandResult Faulted(string? cause = null)
    {
        return new ZoneCommandResult(ZoneCommandResultKind.Faulted, cause);
    }
}
