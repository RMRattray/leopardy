using Leopardy.Models;
using System.Collections.Concurrent;

namespace Leopardy.Services;

public class GameManager
{
    private readonly ConcurrentDictionary<string, Game> _games = new();
    private readonly ConcurrentDictionary<string, string> _connectionToGame = new();
    private readonly object _lock = new();

    public Game CreateGame(string gameName, string hostConnectionId, List<Category> categories, 
        int? maxPlayersPerRound, int? maxPlayersPerGame, CorrectGuesserBehavior correctGuesserBehavior, bool correctGuesserChooses)
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
                    $"{catIndex}-{clueIndex}")).ToDictionary(key => key, _ => false),
            MaxPlayersPerRound = maxPlayersPerRound,
            MaxPlayersPerGame = maxPlayersPerGame,
            CorrectGuesserBehavior = correctGuesserBehavior,
            CorrectGuesserChooses = correctGuesserChooses
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

            // Check max players per game
            if (game.MaxPlayersPerGame.HasValue && game.Players.Count >= game.MaxPlayersPerGame.Value)
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
            InitializeRound(game);
        }

        return true;
    }

    private void InitializeRound(Game game)
    {
        // Shuffle all players
        var shuffledPlayers = game.Players.OrderBy(x => Random.Shared.Next()).ToList();
        
        // Select players for this round
        if (game.MaxPlayersPerRound.HasValue)
        {
            game.PlayersInCurrentRound = shuffledPlayers.Take(game.MaxPlayersPerRound.Value).ToList();
            game.PlayersWaitingForRound = shuffledPlayers.Skip(game.MaxPlayersPerRound.Value).ToList();
        }
        else
        {
            game.PlayersInCurrentRound = shuffledPlayers.ToList();
            game.PlayersWaitingForRound = new List<Player>();
        }

        // Reset control for all players
        foreach (var player in game.Players)
        {
            player.HasControl = false;
        }

        // First player in round gets control
        if (game.PlayersInCurrentRound.Count > 0)
        {
            game.PlayersInCurrentRound[0].HasControl = true;
        }
    }

    public void StartNewRound(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return;

        lock (_lock)
        {
            game.CurrentRound++;
            InitializeRound(game);
        }
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

        var player = game.PlayersInCurrentRound.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (player == null)
            return false; // Player not in current round

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
        var correctGuesser = game.CurrentPlayer;
        
        if (isCorrect)
        {
            correctGuesser.Score += value;
            
            // Handle correct guesser behavior
            if (game.CorrectGuesserBehavior == CorrectGuesserBehavior.Never)
            {
                // Remove from round
                game.PlayersInCurrentRound.Remove(correctGuesser);
                game.PlayersWaitingForRound.Add(correctGuesser);
                correctGuesser.HasControl = false;
                
                // Give control to first player in round
                if (game.PlayersInCurrentRound.Count > 0)
                {
                    game.PlayersInCurrentRound[0].HasControl = true;
                }
            }
            else if (game.CorrectGuesserBehavior == CorrectGuesserBehavior.Leave)
            {
                // Remove from round
                game.PlayersInCurrentRound.Remove(correctGuesser);
                game.PlayersWaitingForRound.Add(correctGuesser);
                correctGuesser.HasControl = false;
                
                // Give control based on CorrectGuesserChooses setting
                if (game.CorrectGuesserChooses)
                {
                    // Correct guesser chooses who gets control (first new player)
                    if (game.PlayersInCurrentRound.Count > 0)
                    {
                        game.PlayersInCurrentRound[0].HasControl = true;
                    }
                }
                else
                {
                    // First new player gets control
                    if (game.PlayersInCurrentRound.Count > 0)
                    {
                        game.PlayersInCurrentRound[0].HasControl = true;
                    }
                }
            }
            else // Stay
            {
                // Keep in round and give control
                correctGuesser.HasControl = true;
                // Reset other players' control
                foreach (var player in game.PlayersInCurrentRound.Where(p => p.ConnectionId != correctGuesser.ConnectionId))
                {
                    player.HasControl = false;
                }
            }
        }
        else
        {
            correctGuesser.Score -= value;
            correctGuesser.HasControl = false;
            
            // If incorrect, control goes to first player in round (if any)
            // The next player in the round gets control
            if (game.PlayersInCurrentRound.Count > 0)
            {
                var currentIndex = game.PlayersInCurrentRound.FindIndex(p => p.ConnectionId == correctGuesser.ConnectionId);
                if (currentIndex >= 0 && currentIndex < game.PlayersInCurrentRound.Count - 1)
                {
                    // Give control to next player
                    game.PlayersInCurrentRound[currentIndex + 1].HasControl = true;
                }
                else if (game.PlayersInCurrentRound.Count > 0)
                {
                    // Wrap around to first player
                    game.PlayersInCurrentRound[0].HasControl = true;
                }
            }
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

