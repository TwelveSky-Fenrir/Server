using Fenrir.Application.Login.Domain;

namespace Fenrir.Application.Login.Tests;

public class LoginCapacityStateTests
{
    [Fact]
    public void DefaultState_MaxPlayersIsNegativeOne_CurrentPlayersIsZero()
    {
        var state = new LoginCapacityState();

        Assert.Equal(-1, state.MaxPlayers);
        Assert.Equal(0, state.CurrentPlayers);
    }

    [Fact]
    public void SetMaxPlayers_UpdatesMaxPlayers_LeavesCurrentPlayersUntouched()
    {
        var state = new LoginCapacityState();
        state.SetCurrentPlayers(42);

        state.SetMaxPlayers(1000);

        Assert.Equal(1000, state.MaxPlayers);
        Assert.Equal(42, state.CurrentPlayers);
    }

    [Fact]
    public void SetCurrentPlayers_UpdatesCurrentPlayers_LeavesMaxPlayersUntouched()
    {
        var state = new LoginCapacityState();
        state.SetMaxPlayers(1000);

        state.SetCurrentPlayers(7);

        Assert.Equal(1000, state.MaxPlayers);
        Assert.Equal(7, state.CurrentPlayers);
    }
}
