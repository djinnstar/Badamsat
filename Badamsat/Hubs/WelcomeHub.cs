using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;


namespace BlazorServerSignalRApp.Server.Hubs
{
    public interface IWelcome
    {
        public Task GoToGame();
        public Task RaiseError();
        public Task ConfirmRunning(List<string> usernames);
        public Task ConfirmNotRunning();
    }

    public class WelcomeHub : Hub<IWelcome>
    {
        public void CanJoinGame(string name)
        {
            var game = Badamsat.Game.LoadOrCreate();
            if (game.usernames.Contains(name) && game.state == Badamsat.Game.State.GettingPlayers)
                Clients.Caller.RaiseError();
            else
                Clients.Caller.GoToGame();

        }

        public async Task CheckGameState()
        {
            var game = Badamsat.Game.LoadOrCreate();
            if (game.state == Badamsat.Game.State.Running)
            {
                List<string> usernames = new List<string>();
                for (int i = 0; i < game.usernames.Count; i++)
                {
                    if (game.connectionIDs[i] == "")
                        usernames.Add(game.usernames[i]);
                }
                await Clients.Caller.ConfirmRunning(usernames);
            }
            else
            {
                await Clients.Caller.ConfirmNotRunning();
            }
        }

    }
}