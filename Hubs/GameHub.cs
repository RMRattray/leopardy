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

    /// <summary>
    /// Join a game as a player (used by the Play screen).
    /// Viewers should use <see cref="JoinView"/> instead.
    /// </summary>
    public async Task JoinGame(string gameId, string playerName)
    {
        var game = _gameManager.GetGame(gameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found");
            return;
        }

        var success = _gameManager.JoinGame(gameId, Context.ConnectionId, playerName);
        
        if (!success)
        {
            await Clients.Caller.SendAsync("Error", "Failed to join game");
            return;
        }

        // Add player to the main game group and broadcast updated player list
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        await Clients.Groups(gameId, gameId + "_viewers").SendAsync("PlayerJoined", game.Players);

        await Clients.Caller.SendAsync("JoinedGame", game.Categories, game.ClueAnswered);
    }

    /// <summary>
    /// Join a game as a viewer (used by the View screen).
    /// Viewers are placed into a dedicated &lt;gameId&gt;_viewers SignalR group
    /// but receive all the same game events as players.
    /// </summary>
    public async Task JoinView(string gameId)
    {
        var game = _gameManager.GetGame(gameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId + "_viewers");

        // Send basic game state so the viewer can render the board immediately
        await Clients.Caller.SendAsync("JoinedGame", game.Categories, game.ClueAnswered);

        // If the game has already started, send the current round / board state
        if (game.Started)
        {
            var playerWithControl = game.PlayersInCurrentRound.FirstOrDefault(p => p.HasControl);
            var firstPlayerName = playerWithControl?.Name 
                ?? (game.PlayersInCurrentRound.Count > 0 ? game.PlayersInCurrentRound[0].Name : "");

            await Clients.Caller.SendAsync(
                "GameStarted",
                firstPlayerName,
                game.PlayersInCurrentRound,
                game.CurrentRound,
                game.PlayersWaitingForRound
            );

            // Send current clue if one is active
            if (game.CurrentClue != null && game.ClueRevealed)
            {
                await Clients.Caller.SendAsync(
                    "ClueSelected",
                    game.CurrentClue.Question,
                    game.CurrentCategory,
                    game.CurrentValue
                );
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

        // Notify players that the game has started
        await Clients.Group(gameId).SendAsync("GameStarted", game.PlayersWaitingForRound);

        // Notify any viewers with richer context so they can show board + players
        var playerWithControl = game.PlayersInCurrentRound.FirstOrDefault(p => p.HasControl);
        var firstPlayerName = playerWithControl?.Name 
            ?? (game.PlayersInCurrentRound.Count > 0 ? game.PlayersInCurrentRound[0].Name : "");

        await Clients.Group(gameId + "_viewers").SendAsync(
            "GameStarted",
            firstPlayerName,
            game.PlayersInCurrentRound,
            game.CurrentRound,
            game.PlayersWaitingForRound
        );

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
            await Clients.Groups(gameId, gameId + "_viewers")
                .SendAsync("ClueSelected", game.CurrentClue.Question, categoryName, value);
            await Clients.Client(game.HostConnectionId).SendAsync("ShowClueCorrectAnswer", game.CurrentClue.Answer);
        }
    }

    public async Task BuzzIn(string gameId)
    {
        var success = _gameManager.BuzzIn(gameId, Context.ConnectionId);
        var game = _gameManager.GetGame(gameId);
        
        if (success && game?.CurrentPlayer != null)
        {
            await Clients.Groups(gameId, gameId + "_viewers")
                .SendAsync("PlayerBuzzedIn", game.CurrentPlayer.Name, game.CurrentPlayer.ConnectionId);
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
            await Clients.Groups(gameId, gameId + "_viewers")
                .SendAsync("AnswerSubmitted", game.CurrentPlayer.Name, game.CurrentAnswer);
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
            await Clients.Groups(gameId, gameId + "_viewers")
                .SendAsync("AnswerJudged", isCorrect, game.Players, clueKey);
            
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
                await Clients.Client(p.ConnectionId).SendAsync("RoundStarted", p.HasControl, true, game.PlayersInCurrentRound, game.PlayersWaitingForRound);
            }
            foreach (Player p in game.PlayersWaitingForRound) {
                await Clients.Client(p.ConnectionId).SendAsync("RoundStarted", false, false, game.PlayersInCurrentRound, game.PlayersWaitingForRound);
            }

            await Clients.Group(gameId + "_viewers")
                .SendAsync("RoundStarted", game.CurrentRound, game.PlayersInCurrentRound, game.PlayersWaitingForRound);
        }
    }

    /// <summary>
    /// Host-only operation to remove a player from the lobby before the game starts.
    /// </summary>
    public async Task RemovePlayer(string gameId, string playerConnectionId)
    {
        var game = _gameManager.GetGame(gameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found");
            return;
        }

        // Only the host for this game can remove players
        if (game.HostConnectionId != Context.ConnectionId)
        {
            await Clients.Caller.SendAsync("Error", "Only the host can remove players");
            return;
        }

        if (game.Started)
        {
            await Clients.Caller.SendAsync("Error", "Cannot remove players after the game has started");
            return;
        }

        _gameManager.RemovePlayer(playerConnectionId);

        // Re-fetch to get the updated players list and broadcast
        game = _gameManager.GetGame(gameId);
        if (game != null)
        {
            await Clients.Groups(gameId, gameId + "_viewers")
                .SendAsync("PlayerJoined", game.Players);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _gameManager.RemovePlayer(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

