using Leopardy.Models;
using System.Collections.Concurrent;

namespace Leopardy.Services;

public class GameManager
{
    private readonly ConcurrentDictionary<string, Game> _games = new();
    private readonly ConcurrentDictionary<string, string> _connectionToGame = new();
    private readonly object _lock = new();

    public Game CreateGame(string gameName, string hostConnectionId, List<Category> categories, 
        int? maxPlayersPerRound, int? maxPlayersPerGame, CorrectGuesserBehavior correctGuesserBehavior, bool correctGuesserChooses, int? answerTimeLimitSeconds, int? roundMaxDuration, bool clueStaysOnRoundTimeout)
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
            CorrectGuesserChooses = correctGuesserBehavior == CorrectGuesserBehavior.Stay ? correctGuesserChooses : false,
            AnswerTimeLimitSeconds = answerTimeLimitSeconds,
            RoundMaxDuration = roundMaxDuration,
            ClueStaysOnRoundTimeOut = clueStaysOnRoundTimeout
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

            if (game.Players.Count == 0) return false;

            game.Started = true;
            
            // Initial distribution of players in round
            int i = 0;
            while ((!game.MaxPlayersPerRound.HasValue || i < game.MaxPlayersPerRound) && i < game.Players.Count) {
                game.PlayersInCurrentRound.Add(game.Players[i]);
                ++i;
            }
            while (i < game.Players.Count) {
                game.PlayersWaitingForRound.Add(game.Players[i]);
                ++i;
            }

            game.PlayersInCurrentRound[0].HasControl = true;
            game.PlayersNotBuzzedInCurrentRound = game.PlayersInCurrentRound.Count;
        }

        return true;
    }

    // Set who is in the next round, drawing from pool of players
    public void InitializeRound(Game game, Player? winner)
    {

        // Reset control for all players
        foreach (var player in game.Players)
        {
            player.HasControl = false;
        }

        // Only necessary if players in a round is capped at fewer than total players
        if (game.MaxPlayersPerRound.HasValue && game.Players.Count > game.MaxPlayersPerRound) {
            switch (game.CorrectGuesserBehavior) {

                // In "never" behavior, remove everyone from that round and start from the top of the list
                case CorrectGuesserBehavior.Never:
                    foreach (Player p in game.PlayersInCurrentRound) {
                        game.PlayersWaitingForRound.Add(p);
                    }
                    game.PlayersInCurrentRound = [];
                    for (int i = 0; i < game.MaxPlayersPerRound; ++i) {
                        game.PlayersInCurrentRound.Add(game.PlayersWaitingForRound[0]);
                        game.PlayersWaitingForRound.Remove(game.PlayersWaitingForRound[0]);
                    }
                break;

                // In "leave" behavior, remove the correct guesser only
                case CorrectGuesserBehavior.Leave:
                    if (winner != null) {
                        game.PlayersWaitingForRound.Add(winner);
                        game.PlayersInCurrentRound.Remove(winner);
                        game.PlayersInCurrentRound.Add(game.PlayersWaitingForRound[0]);
                        game.PlayersWaitingForRound.Remove(game.PlayersWaitingForRound[0]);
                    }
                break;

                // In "stay" behavior, remove incorrect guessers
                case CorrectGuesserBehavior.Stay:
                    foreach (Player p in game.PlayersInCurrentRound) {
                        if (p != winner) {
                            game.PlayersWaitingForRound.Add(p);
                        }
                    }
                    game.PlayersInCurrentRound = (winner != null) ? [ winner ] : [];
                    while (game.PlayersInCurrentRound.Count < game.MaxPlayersPerRound) {
                        game.PlayersInCurrentRound.Add(game.PlayersWaitingForRound[0]);
                        game.PlayersWaitingForRound.Remove(game.PlayersWaitingForRound[0]);
                    }

                break;
            }
        }

        // First player in round gets control
        if (winner != null && game.CorrectGuesserChooses) {
            winner.HasControl = true;
        }
        else
        {
            game.PlayersInCurrentRound[0].HasControl = true;
        }

        game.PlayersNotBuzzedInCurrentRound = game.PlayersInCurrentRound.Count;

        // Reset clue state
        game.CurrentClue = null;
        game.CurrentCategory = null;
        game.CurrentValue = null;
        game.ClueRevealed = false;
        game.WaitingForAnswer = false;
        game.CurrentPlayer = null;
        game.CurrentAnswer = null;
        game.BuzzTime = null;
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
        game.RoundOver = false;
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
            game.PlayersNotBuzzedInCurrentRound -= 1;
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
        }
        else
        {
            correctGuesser.Score -= value;
        }

        game.WaitingForAnswer = false;
        game.CurrentPlayer = null;
        if (isCorrect || game.PlayersNotBuzzedInCurrentRound == 0 || game.RoundOver) {
            game.ClueAnswered[clueKey] = true;
            
            InitializeRound(game, correctGuesser);
            return clueKey;
        }
        
        return null;
    }

    public string RemovePlayer(string connectionId)
    {
        if (_connectionToGame.TryRemove(connectionId, out var gameId))
        {
            if (_games.TryGetValue(gameId, out var game))
            {
                lock (_lock)
                {
                    
                    game.Players.RemoveAll(p => p.ConnectionId == connectionId);

                    // Can need to remove player after game has started (on their disconnection)
                    if (game.Started) {
                        
                        var departer = game.PlayersInCurrentRound.FirstOrDefault(p => p.ConnectionId == connectionId);
                        if (departer != null) {
                            // Oh dear - a player in the current round disconnected
                            
                            game.PlayersInCurrentRound.FirstOrDefault(p => p.ConnectionId == connectionId);
                            if (game.PlayersWaitingForRound.Count > 0) {
                                game.PlayersInCurrentRound.Add(game.PlayersWaitingForRound[0]);
                                game.PlayersWaitingForRound.Remove(game.PlayersWaitingForRound[0]);
                            }
                            if (departer.HasControl && game.CurrentClue == null) {
                                game.PlayersInCurrentRound[0].HasControl = true;
                            }
                        }
                        else {
                            game.PlayersWaitingForRound.RemoveAll(p => p.ConnectionId == connectionId);
                        }
                    }
                }
                return game.GameId;
            }
        }
        return "";
    }

    public string? MarkClueAsAnsweredAndStartNewRound(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return null;

        lock (_lock)
        {
            if (game.CurrentCategory == null || game.CurrentValue == null)
                return null;

            var categoryIndex = game.Categories.FindIndex(c => c.Name == game.CurrentCategory);
            if (categoryIndex < 0)
                return null;

            var category = game.Categories[categoryIndex];
            var clueIndex = category.Clues.FindIndex(c => c.Value == game.CurrentValue);
            if (clueIndex < 0)
                return null;

            var clueKey = $"{categoryIndex}-{clueIndex}";

            // Start new round with no winner
            if (!game.ClueStaysOnRoundTimeOut) game.ClueAnswered[clueKey] = true;
            InitializeRound(game, null);
            game.CurrentRound++;

            return clueKey;
        }
    }

    private string GenerateGameCode()
    {
        var random = new Random();
        return random.Next(1000, 10000).ToString();
    }
}

