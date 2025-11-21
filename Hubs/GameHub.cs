using Microsoft.AspNetCore.SignalR;
using Leopardy.Services;
using Leopardy.Models;

namespace Leopardy.Hubs;

public class GameHub : Hub
{
    private readonly GameManager _gameManager;

    public GameHub(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public async Task CreateGame(string gameName, string templateName, int? maxPlayersPerRound, int? maxPlayersPerGame, 
        int correctGuesserBehavior, bool correctGuesserChooses)
    {
        var templates = GameDataService.GetGameTemplates();
        var template = templates.FirstOrDefault(t => t.Name == templateName);
        
        if (template == null)
        {
            await Clients.Caller.SendAsync("Error", "Game template not found");
            return;
        }

        // Validate max players constraints
        if (maxPlayersPerGame.HasValue && maxPlayersPerRound.HasValue && maxPlayersPerRound.Value > maxPlayersPerGame.Value)
        {
            await Clients.Caller.SendAsync("Error", "Max players per round cannot exceed max players per game");
            return;
        }

        var behavior = (CorrectGuesserBehavior)correctGuesserBehavior;
        var game = _gameManager.CreateGame(gameName, Context.ConnectionId, template.Categories, 
            maxPlayersPerRound, maxPlayersPerGame, behavior, correctGuesserChooses);
        await Clients.Caller.SendAsync("GameCreated", game.GameId, game.Categories);
        await Groups.AddToGroupAsync(Context.ConnectionId, game.GameId);
    }

    public async Task CreateGameWithCategories(string gameName, object categoriesData, int? maxPlayersPerRound, int? maxPlayersPerGame, 
        int correctGuesserBehavior, bool correctGuesserChooses)
    {
        // Validate max players constraints
        if (maxPlayersPerGame.HasValue && maxPlayersPerRound.HasValue && maxPlayersPerRound.Value > maxPlayersPerGame.Value)
        {
            await Clients.Caller.SendAsync("Error", "Max players per round cannot exceed max players per game");
            return;
        }

        // Deserialize categories from JSON
        List<Category> categories;
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(categoriesData);
            categories = System.Text.Json.JsonSerializer.Deserialize<List<Category>>(json) ?? new List<Category>();
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Error parsing categories: {ex.Message}");
            return;
        }

        if (categories.Count == 0)
        {
            await Clients.Caller.SendAsync("Error", "No categories provided");
            return;
        }

        var behavior = (CorrectGuesserBehavior)correctGuesserBehavior;
        var game = _gameManager.CreateGame(gameName, Context.ConnectionId, categories, 
            maxPlayersPerRound, maxPlayersPerGame, behavior, correctGuesserChooses);
        await Clients.Caller.SendAsync("GameCreated", game.GameId, game.Categories);
        await Groups.AddToGroupAsync(Context.ConnectionId, game.GameId);
    }

    public async Task JoinGame(string gameId, string playerName)
    {
        var game = _gameManager.GetGame(gameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found");
            return;
        }

        // Allow viewers to join even after game has started
        bool isViewer = playerName == "Viewer";
        
        if (!isViewer)
        {
            var success = _gameManager.JoinGame(gameId, Context.ConnectionId, playerName);
            
            if (!success)
            {
                await Clients.Caller.SendAsync("Error", "Failed to join game");
                return;
            }
            
            await Clients.Group(gameId).SendAsync("PlayerJoined", game.Players);

            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        }
        else {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId + "_viewers");
        }
        
        await Clients.Caller.SendAsync("JoinedGame", game.Categories, game.ClueAnswered);
        
        // If game has started, send current state to viewer
        if (isViewer && game.Started)
        {
            var playerWithControl = game.PlayersInCurrentRound.FirstOrDefault(p => p.HasControl);
            var firstPlayerName = playerWithControl?.Name ?? (game.PlayersInCurrentRound.Count > 0 ? game.PlayersInCurrentRound[0].Name : "");
            await Clients.Caller.SendAsync("GameStarted", firstPlayerName, game.PlayersInCurrentRound, game.CurrentRound, game.PlayersWaitingForRound);
            
            // Send current clue if one is active
            if (game.CurrentClue != null && game.ClueRevealed)
            {
                await Clients.Caller.SendAsync("ClueSelected", game.CurrentClue.Question, game.CurrentCategory, game.CurrentValue);
            }
        }
    }

    public async Task StartGame(string gameId)
    {
        var success = _gameManager.StartGame(gameId);

        if (!success)
        {
            await Clients.Caller.SendAsync("Error", "Failed to start game");
            return;
        }

        var game = _gameManager.GetGame(gameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found");
            return;
        }

        await Clients.Group(gameId).SendAsync("GameStarted", game.PlayersWaitingForRound);
        await StartNewRound(gameId);
    }

    public async Task SelectClue(string gameId, string categoryName, int value)
    {
        var game = _gameManager.GetGame(gameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found");
            return;
        }

        // Check if caller has control
        var player = game.PlayersInCurrentRound.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (player == null || !player.HasControl)
        {
            await Clients.Caller.SendAsync("Error", "You don't have control to select a clue");
            return;
        }

        _gameManager.SelectClue(gameId, categoryName, value);
        game = _gameManager.GetGame(gameId);
        
        if (game?.CurrentClue != null)
        {
            await Clients.Group(gameId).SendAsync("ClueSelected", game.CurrentClue.Question, categoryName, value);
            await Clients.Client(game.HostConnectionId).SendAsync("ShowClueCorrectAnswer", game.CurrentClue.Answer);
        }
    }

    public async Task BuzzIn(string gameId)
    {
        var success = _gameManager.BuzzIn(gameId, Context.ConnectionId);
        var game = _gameManager.GetGame(gameId);
        
        if (success && game?.CurrentPlayer != null)
        {
            await Clients.Group(gameId).SendAsync("PlayerBuzzedIn", game.CurrentPlayer.Name, game.CurrentPlayer.ConnectionId);
        }
        else if (!success)
        {
            await Clients.Caller.SendAsync("BuzzFailed", "Unable to buzz in");
        }
    }

    public async Task SubmitAnswer(string gameId, string answer)
    {
        _gameManager.SubmitAnswer(gameId, Context.ConnectionId, answer);
        var game = _gameManager.GetGame(gameId);
        
        if (game?.CurrentPlayer != null && game.CurrentAnswer != null)
        {
            await Clients.Group(gameId).SendAsync("AnswerSubmitted", game.CurrentPlayer.Name, game.CurrentAnswer);
        }
    }

    public async Task JudgeAnswer(string gameId, bool isCorrect)
    {
        var game = _gameManager.GetGame(gameId);
        string? correctAnswer = null;
        
        if (game?.CurrentClue != null)
        {
            correctAnswer = game.CurrentClue.Answer;
        }
        
        var clueKey = _gameManager.JudgeAnswer(gameId, isCorrect);
        game = _gameManager.GetGame(gameId);
        
        if (game != null)
        {
            await Clients.Group(gameId).SendAsync("AnswerJudged", isCorrect, game.Players, clueKey, game.PlayersInCurrentRound, game.PlayersWaitingForRound);
            
            if (clueKey != null)
            {
                await StartNewRound(gameId);
            }
        }
    }

    public async Task StartNewRound(string gameId)
    {
        var game = _gameManager.GetGame(gameId);
        
        if (game != null)
        {
            var playerWithControl = game.PlayersInCurrentRound.FirstOrDefault(p => p.HasControl);
            foreach (Player p in game.PlayersInCurrentRound) {
                await Clients.Client(p.ConnectionId).SendAsync("RoundStarted", p.HasControl, true, game.PlayersWaitingForRound);
            }
            foreach (Player p in game.PlayersWaitingForRound) {
                await Clients.Client(p.ConnectionId).SendAsync("RoundStarted", false, false, game.PlayersWaitingForRound);
            }

            await Clients.Group(gameId + "_viewers").SendAsync("RoundStarted", 0, game.PlayersInCurrentRound, game.PlayersWaitingForRound);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _gameManager.RemovePlayer(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

