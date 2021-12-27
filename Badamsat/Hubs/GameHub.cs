using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;


namespace BlazorServerSignalRApp.Server.Hubs
{
    public interface IGame
    {
        Task UpdateBoard(string encoded);
        Task SetID(int id);
        Task SetUsernames(List<string> usernames);
        Task EndGame(string winner);
    }

    public class GameHub : Hub<IGame>
    {
        public async Task RequestID(string name)
        {
            Badamsat.Game game = Badamsat.Game.LoadOrCreate();
            if (game.state != 0)
            {
                game = new Badamsat.Game(new List<string>(), false);
                game.state = 0;
            }
            game.usernames.Add(name);
            await Clients.Caller.SetID(game.numPlayers - 1);
            await Clients.All.UpdateBoard(game.Stringify());
            game.Save();
        }

        public async Task StartGame()
        {
            var game2 = Badamsat.Game.LoadOrCreate();
            var game = new Badamsat.Game(game2.usernames, true);
            game.Save();
            await Clients.All.UpdateBoard(game.Stringify());
        }

        public async Task ChoosePlay(int pileIndex, int cardIndex, int userID)
        {
            var game = Badamsat.Game.LoadOrCreate();
            if (userID == game.currentPlayerID)
            {
                game.Turn(userID, cardIndex, pileIndex);
                game.Save();
            }
            await Clients.All.UpdateBoard(game.Stringify());
            if (game.state == 2)
            {
                await Clients.All.EndGame(game.usernames[userID]);
            }
        }

        public async Task Pass()
        {
            var game = Badamsat.Game.LoadOrCreate();
            game.mostRecentPlays.Add((game.currentPlayerID, null));
            game.currentPlayerID = (game.currentPlayerID + 1) % game.numPlayers;
            game.Save();
            await Clients.All.UpdateBoard(game.Stringify());
        }

        public async Task GetEndGame()
        {
            var game = Badamsat.Game.LoadOrCreate();
            await Clients.Caller.UpdateBoard(game.Stringify());
        }
    }
}