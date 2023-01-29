using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using System.Runtime;


namespace BlazorServerSignalRApp.Server.Hubs
{
    public interface IGame
    {
        Task UpdateBoard(string encoded);
        Task SetID(int id);
        Task EndGame(string winner);
        Task JoinGame(string username);
    }

    public class GameHub : Hub<IGame>
    {
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Badamsat.Game game = Badamsat.Game.LoadOrCreate();
            if (game.state == Badamsat.Game.State.GettingPlayers)
            {
                int j = game.connectionIDs.IndexOf(Context.ConnectionId);
                game.connectionIDs.RemoveAt(j);
                game.usernames.RemoveAt(j);
                await Clients.All.UpdateBoard(game.Stringify());
                game.Save();
            }
            if (game.state == Badamsat.Game.State.Running)
            {
                game.connectionIDs[game.connectionIDs.IndexOf(Context.ConnectionId)] = "";
                if (game.connectionIDs.Count == game.connectionIDs.Where(x => x == "").Count())
                    game.Delete();
                else
                    game.Save();
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinGame(string username)
        {
            Badamsat.Game game = Badamsat.Game.LoadOrCreate();
            
            if (game.state == Badamsat.Game.State.Completed)
                game = new Badamsat.Game(new List<string>(), new List<string>(), false);
            if (game.state == Badamsat.Game.State.Running && game.usernames.Contains(username))
            {
                game.connectionIDs[game.usernames.IndexOf(username)] = Context.ConnectionId;
                await Clients.All.UpdateBoard(game.Stringify());
                game.Save();
                return;
            }


            game.usernames.Add(username);
            game.connectionIDs.Add(Context.ConnectionId);
            await Clients.All.UpdateBoard(game.Stringify());
            game.Save();
        }

        public async Task StartGame(bool showScores)
        {
            var game2 = Badamsat.Game.LoadOrCreate();
            var game = new Badamsat.Game(game2.usernames, game2.connectionIDs, true);
            game.showScores = showScores;
            await Clients.All.UpdateBoard(game.Stringify());
            game.Save();
        }

        public async Task ChoosePlay(int pileIndex, int cardIndex, int userID)
        {
            var game = Badamsat.Game.LoadOrCreate();
            if (userID == game.currentPlayerNum)
            {
                game.Turn(userID, cardIndex, pileIndex);
                game.Save();
            }
            await Clients.All.UpdateBoard(game.Stringify());
            if (game.state == Badamsat.Game.State.Completed)
            {
                await Clients.All.EndGame(game.usernames[userID]);
            }
        }

        public async Task Pass()
        {
            var game = Badamsat.Game.LoadOrCreate();
            game.mostRecentPlays.Add((game.currentPlayerNum, null));
            game.currentPlayerNum = (game.currentPlayerNum + 1) % game.numPlayers;
            game.Save();
            if (game.mostRecentPlays.Count > game.numPlayers)
                game.mostRecentPlays = game.mostRecentPlays.GetRange(game.mostRecentPlays.Count - game.numPlayers, game.numPlayers);
            await Clients.All.UpdateBoard(game.Stringify());
        }

        public async Task GetEndGame()
        {
            var game = Badamsat.Game.LoadOrCreate();
            await Clients.Caller.UpdateBoard(game.Stringify());
        }
    }
}