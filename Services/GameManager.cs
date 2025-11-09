using Leopardy.Models;
using System.Collections.Concurrent;

namespace Leopardy.Services;

public class GameManager
{
    private readonly ConcurrentDictionary<string, Game> _games = new();
    private readonly ConcurrentDictionary<string, string> _connectionToGame = new();
    private readonly object _lock = new();

    public Game CreateGame(string gameName, string hostConnectionId, List<Category> categories)
    {
        var gameId = GenerateGameCode();
        var game = new Game
        {
            GameId = gameId,
            GameName = gameName,
            HostConnectionId = hostConnectionId,
            Categories = categories,
            ClueAnswered = categories.SelectMany((cat, catIndex) =>
                cat.Clues.Select((clue, clueIndex) =>
                    $"{catIndex}-{clueIndex}")).ToDictionary(key => key, _ => false)
        };

        _games.TryAdd(gameId, game);
        _connectionToGame.TryAdd(hostConnectionId, gameId);
        return game;
    }

    public bool JoinGame(string gameId, string connectionId, string playerName)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return false;

        lock (_lock)
        {
            if (game.Started) return false;

            if (game.Players.Any(p => p.ConnectionId == connectionId))
                return false;

            var player = new Player
            {
                ConnectionId = connectionId,
                Name = playerName,
                Score = 0
            };

            game.Players.Add(player);
            _connectionToGame.TryAdd(connectionId, gameId);
        }

        return true;
    }

    public bool StartGame(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return false;

        lock (_lock)
        {
            if (game.Started) return false;

            game.Started = true;
        }

        return true;
    }

    public Game? GetGame(string gameId)
    {
        _games.TryGetValue(gameId, out var game);
        return game;
    }

    public Game? GetGameByConnection(string connectionId)
    {
        if (_connectionToGame.TryGetValue(connectionId, out var gameId))
        {
            return GetGame(gameId);
        }
        return null;
    }

    public void SelectClue(string gameId, string categoryName, int value)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return;

        var category = game.Categories.FirstOrDefault(c => c.Name == categoryName);
        if (category == null)
            return;

        var clue = category.Clues.FirstOrDefault(c => c.Value == value);
        if (clue == null)
            return;

        var categoryIndex = game.Categories.IndexOf(category);
        var clueIndex = category.Clues.IndexOf(clue);
        var clueKey = $"{categoryIndex}-{clueIndex}";

        if (game.ClueAnswered.ContainsKey(clueKey) && game.ClueAnswered[clueKey])
            return; // Already answered

        game.CurrentClue = clue;
        game.CurrentCategory = categoryName;
        game.CurrentValue = value;
        game.ClueRevealed = true;
        game.WaitingForAnswer = false;
        game.CurrentPlayer = null;
        game.CurrentAnswer = null;
        game.BuzzTime = null;
    }

    public bool BuzzIn(string gameId, string connectionId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return false;

        if (game.CurrentClue == null || !game.ClueRevealed)
            return false;

        var player = game.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (player == null)
            return false;

        lock (_lock)
        {
            if (game.WaitingForAnswer)
                return false; // Someone already buzzed in

            game.WaitingForAnswer = true;
            game.CurrentPlayer = player;
            game.BuzzTime = DateTime.UtcNow;
            player.LastBuzzTime = DateTime.UtcNow;
        }

        return true;
    }

    public void SubmitAnswer(string gameId, string connectionId, string answer)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return;

        if (game.CurrentPlayer?.ConnectionId != connectionId)
            return;

        game.CurrentAnswer = answer;
    }

    public string? JudgeAnswer(string gameId, bool isCorrect)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return null;

        if (game.CurrentPlayer == null || game.CurrentClue == null || game.CurrentCategory == null || game.CurrentValue == null)
            return null;

        var categoryIndex = game.Categories.FindIndex(c => c.Name == game.CurrentCategory);
        if (categoryIndex < 0)
            return null;
            
        var category = game.Categories[categoryIndex];
        var clueIndex = category.Clues.FindIndex(c => c.Value == game.CurrentValue);
        if (clueIndex < 0)
            return null;
            
        var clueKey = $"{categoryIndex}-{clueIndex}";

        var value = game.CurrentValue.Value;
        
        if (isCorrect)
        {
            game.CurrentPlayer.Score += value;
            game.CurrentPlayer.HasControl = true;
            // Reset other players' control
            foreach (var player in game.Players.Where(p => p.ConnectionId != game.CurrentPlayer.ConnectionId))
            {
                player.HasControl = false;
            }
        }
        else
        {
            game.CurrentPlayer.Score -= value;
            game.CurrentPlayer.HasControl = false;
        }

        game.ClueAnswered[clueKey] = true;
        game.WaitingForAnswer = false;
        
        return clueKey;
    }

    public void ResetClue(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return;

        game.CurrentClue = null;
        game.CurrentCategory = null;
        game.CurrentValue = null;
        game.ClueRevealed = false;
        game.WaitingForAnswer = false;
        game.CurrentPlayer = null;
        game.CurrentAnswer = null;
        game.BuzzTime = null;
    }

    public void RemovePlayer(string connectionId)
    {
        if (_connectionToGame.TryRemove(connectionId, out var gameId))
        {
            if (_games.TryGetValue(gameId, out var game))
            {
                lock (_lock)
                {
                    game.Players.RemoveAll(p => p.ConnectionId == connectionId);
                }
            }
        }
    }

    private string GenerateGameCode()
    {
        var random = new Random();
        return random.Next(1000, 10000).ToString();
    }
}

