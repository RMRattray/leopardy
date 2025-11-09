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

    public async Task CreateGame(string gameName, string templateName)
    {
        var templates = GameDataService.GetGameTemplates();
        var template = templates.FirstOrDefault(t => t.Name == templateName);
        
        if (template == null)
        {
            await Clients.Caller.SendAsync("Error", "Game template not found");
            return;
        }

        var game = _gameManager.CreateGame(gameName, Context.ConnectionId, template.Categories);
        await Clients.Caller.SendAsync("GameCreated", game.GameId, game.Categories);
        await Groups.AddToGroupAsync(Context.ConnectionId, game.GameId);
    }

    public async Task JoinGame(string gameId, string playerName)
    {
        var success = _gameManager.JoinGame(gameId, Context.ConnectionId, playerName);
        
        if (!success)
        {
            await Clients.Caller.SendAsync("Error", "Failed to join game");
            return;
        }

        var game = _gameManager.GetGame(gameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        await Clients.Caller.SendAsync("JoinedGame", game.Categories, game.ClueAnswered);
        await Clients.Group(gameId).SendAsync("PlayerJoined", game.Players);
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

        await Clients.Group(gameId).SendAsync("GameStarted", game.Players[0].Name);
    }

    public async Task SelectClue(string gameId, string categoryName, int value)
    {
        _gameManager.SelectClue(gameId, categoryName, value);
        var game = _gameManager.GetGame(gameId);
        
        if (game?.CurrentClue != null)
        {
            await Clients.Group(gameId).SendAsync("ClueSelected", game.CurrentClue.Question, categoryName, value);
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
        
        if (game != null && clueKey != null)
        {
            if (isCorrect && correctAnswer != null)
            {
                await Clients.Group(gameId).SendAsync("ShowAnswer", correctAnswer);
                await Clients.Group(gameId).SendAsync("PlayerSelected", game.CurrentPlayer.Name);
            }

            await Clients.Group(gameId).SendAsync("AnswerJudged", isCorrect, game.Players, clueKey);
            
            // Reset clue after a delay
            _gameManager.ResetClue(gameId);
        }
    }

    public async Task ResetClue(string gameId)
    {
        _gameManager.ResetClue(gameId);
        await Clients.Group(gameId).SendAsync("ClueReset");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _gameManager.RemovePlayer(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

