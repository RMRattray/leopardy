using Microsoft.AspNetCore.SignalR;
using Leopardy.Services;
using Leopardy.Models;
using System.Collections.Concurrent;

namespace Leopardy.Hubs;

public class GameHub : Hub
{
    private readonly GameService _gameService;

    public GameHub(GameService gameService)
    {
        _gameService = gameService;
    }

    public async Task CreateGame(string gameName, string templateName, int? maxPlayersPerRound, int? maxPlayersPerGame, 
        int correctGuesserBehavior, bool correctGuesserChooses, int? roundMaxDuration, int? answerTimeLimitSeconds)
    {
        await _gameService.CreateGame(Context.ConnectionId, gameName, templateName, maxPlayersPerRound, maxPlayersPerGame,
            correctGuesserBehavior, correctGuesserChooses, roundMaxDuration, answerTimeLimitSeconds);
    }

    public async Task CreateGameWithCategories(string gameName, object categoriesData, int? maxPlayersPerRound, int? maxPlayersPerGame, 
        int correctGuesserBehavior, bool correctGuesserChooses, int? roundMaxDuration, int? answerTimeLimitSeconds)
    {
        await _gameService.CreateGameWithCategories(Context.ConnectionId, gameName, categoriesData, maxPlayersPerRound, maxPlayersPerGame,
            correctGuesserBehavior, correctGuesserChooses, roundMaxDuration, answerTimeLimitSeconds);
    }

    public async Task JoinGame(string gameId, string playerName)
    {
        await _gameService.JoinGame(Context.ConnectionId, gameId, playerName);
    }

    public async Task JoinView(string gameId)
    {
        await _gameService.JoinView(Context.ConnectionId, gameId);
    }

    public async Task StartGame(string gameId)
    {
        await _gameService.StartGame(Context.ConnectionId, gameId);
    }

    public async Task SelectClue(string gameId, string categoryName, int value)
    {
        await _gameService.SelectClue(Context.ConnectionId, gameId, categoryName, value);
    }

    public async Task BuzzIn(string gameId)
    {
        await _gameService.BuzzIn(Context.ConnectionId, gameId);
    }

    public async Task SubmitAnswer(string gameId, string answer)
    {
        await _gameService.SubmitAnswer(Context.ConnectionId, gameId, answer);
    }

    public async Task JudgeAnswer(string gameId, bool isCorrect)
    {
        await _gameService.JudgeAnswer(gameId, isCorrect);
    }

    public async Task StartNewRound(string gameId)
    {
        await _gameService.StartNewRound(gameId);
    }

    public async Task RemovePlayer(string gameId, string playerConnectionId)
    {
        await _gameService.RemovePlayer(Context.ConnectionId, gameId, playerConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _gameService.OnDisconnectedAsync(Context.ConnectionId, exception);
        await base.OnDisconnectedAsync(exception);
    }
}

