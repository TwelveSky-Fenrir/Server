namespace Fenrir.Application.Game.Hosting;

public sealed class GameConnectionReadiness
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        return _completion.Task.WaitAsync(cancellationToken);
    }

    public void MarkReady()
    {
        _completion.TrySetResult();
    }

    public void MarkFailed(Exception exception)
    {
        _completion.TrySetException(exception);
    }
}
