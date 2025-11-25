using Microsoft.AspNetCore.SignalR;
using Leopardy.Services;
using Leopardy.Models;
using System.Collections.Concurrent;

namespace Leopardy.Hubs;

public class GameHub : Hub
{
    private readonly GameManager _gameManager;
    // Track active timers per game - only one timer active at a time per game
    // When clue is selected: clue timer starts
    // When player buzzes in: clue timer stops, answer timer starts
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _gameRoundTimers = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _gameAnswerTimers = new();

    public GameHub(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public async Task CreateGame(string gameName, string templateName, int? maxPlayersPerRound, int? maxPlayersPerGame, 
        int correctGuesserBehavior, bool correctGuesserChooses, int? roundMaxDuration, int? answerTimeLimitSeconds)
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
            maxPlayersPerRound, maxPlayersPerGame, behavior, correctGuesserChooses, answerTimeLimitSeconds, roundMaxDuration);
        await Clients.Caller.SendAsync("GameCreated", game.GameId, game.Categories);
        await Groups.AddToGroupAsync(Context.ConnectionId, game.GameId);
    }

    public async Task CreateGameWithCategories(string gameName, object categoriesData, int? maxPlayersPerRound, int? maxPlayersPerGame, 
        int correctGuesserBehavior, bool correctGuesserChooses, int? roundMaxDuration, int? answerTimeLimitSeconds)
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
            maxPlayersPerRound, maxPlayersPerGame, behavior, correctGuesserChooses, roundMaxDuration, answerTimeLimitSeconds);
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

            // Start timer for clue if time limit is set
            if (game.RoundMaxDuration.HasValue)
            {
                StartRoundTimer(gameId, game.RoundMaxDuration.Value);
            }
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

            // Start timer for answer if time limit is set
            if (game.AnswerTimeLimitSeconds.HasValue)
            {
                StartAnswerTimer(gameId, game.AnswerTimeLimitSeconds.Value, game.CurrentPlayer.ConnectionId);
            }
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
            // Cancel the answer timer since the player has submitted their answer
            CancelAnswerTimer(gameId);
            
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
        // Cancel any active timer when starting a new round
        CancelRoundTimer(gameId);

        var game = _gameManager.GetGame(gameId);
        
        if (game != null)
        {

            var playerWithControl = game.PlayersInCurrentRound.FirstOrDefault(p => p.HasControl);
            foreach (Player p in game.PlayersInCurrentRound) {
                Console.WriteLine($"Hello-o!  Earth to {p.Name} at {p.ConnectionId}");
                await Clients.Client(p.ConnectionId).SendAsync("RoundStarted", p.HasControl, true, game.PlayersInCurrentRound, game.PlayersWaitingForRound);
            }
            foreach (Player p in game.PlayersWaitingForRound) {
                Console.WriteLine($"Hello-o!  Earth to {p.Name} at {p.ConnectionId}");
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

        await Groups.RemoveFromGroupAsync(playerConnectionId, gameId);
        await Clients.Client(playerConnectionId).SendAsync("PlayerRemoved");

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

    private void CancelRoundTimer(string gameId)
    {
        if (_gameRoundTimers.TryRemove(gameId, out var cts))
        {
            try
            {
                cts.Cancel();
            }
            catch { }
            finally
            {
                cts.Dispose();
            }
        }
    }

    private void CancelAnswerTimer(string gameId)
    {
        if (_gameAnswerTimers.TryRemove(gameId, out var cts))
        {
            try
            {
                cts.Cancel();
            }
            catch { }
            finally
            {
                cts.Dispose();
            }
        }
    }

    /// <summary>
    /// Starts the clue timer when a clue is selected.
    /// This timer expires if no player buzzes in within the time limit.
    /// When expired, the clue is marked as answered and a new round starts (no winner).
    /// </summary>
    private void StartRoundTimer(string gameId, int timeLimitSeconds)
    {
        // Cancel any existing timer (shouldn't be one, but be safe)
        CancelRoundTimer(gameId);

        var cts = new CancellationTokenSource();
        if (!_gameRoundTimers.TryAdd(gameId, cts))
        {
            cts.Dispose();
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(timeLimitSeconds), cts.Token);
                
                // Clue timer expired - check game state
                var game = _gameManager.GetGame(gameId);
                if (game == null) return;

                // Verify this clue timer is still the active timer (hasn't been replaced by answer timer)
                if (!_gameRoundTimers.TryGetValue(gameId, out var activeCts) || activeCts != cts)
                    return;

                // If no one has buzzed in and clue is still active, start next round as if there was no winner
                if (!game.WaitingForAnswer && game.CurrentPlayer == null)
                {

                    // Mark clue as answered and start new round
                    if (_gameManager.MarkClueAsAnsweredAndStartNewRound(gameId))
                    {
                        _gameRoundTimers.TryRemove(gameId, out _);
                        await StartNewRound(gameId);
                    }
                }
                else {
                    game.RoundOver = true;
                }
            }
            catch (OperationCanceledException)
            {
                // Timer was cancelled (likely because someone buzzed in and answer timer started)
            }
            finally
            {
                cts.Dispose();
            }
        });
    }

    /// <summary>
    /// Starts the answer timer when a player buzzes in.
    /// This timer expires if the player doesn't submit an answer within the time limit.
    /// When expired, it's treated as a wrong answer and processed accordingly.
    /// </summary>
    private void StartAnswerTimer(string gameId, int timeLimitSeconds, string playerConnectionId)
    {
        // Cancel the clue timer (if it was running) and start the answer timer
        CancelAnswerTimer(gameId);

        var cts = new CancellationTokenSource();
        if (!_gameAnswerTimers.TryAdd(gameId, cts))
        {
            cts.Dispose();
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(timeLimitSeconds), cts.Token);

                Console.WriteLine("The start answer timer task runs after some time");
                
                // Answer timer expired - treat as wrong answer
                var game = _gameManager.GetGame(gameId);
                if (game == null) return;

                // Verify this answer timer is still the active timer (hasn't been cancelled)
                if (!_gameAnswerTimers.TryGetValue(gameId, out var activeCts) || activeCts != cts)
                    return;

                // Only process timeout if this player is still the current player and waiting for answer
                if (game.CurrentPlayer?.ConnectionId == playerConnectionId && game.WaitingForAnswer)
                {
                    // Send timeout signal to the buzzed-in player
                    await Clients.Client(playerConnectionId).SendAsync("AnswerTimeout");

                    // Process as wrong answer
                    _gameAnswerTimers.TryRemove(gameId, out _);
                    var clueKey = _gameManager.JudgeAnswer(gameId, false);
                    game = _gameManager.GetGame(gameId);
                    
                    if (game != null)
                    {
                        await Clients.Groups(gameId, gameId + "_viewers")
                            .SendAsync("AnswerJudged", false, game.Players, clueKey);
                        
                        if (clueKey != null)
                        {
                            await StartNewRound(gameId);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timer was cancelled (answer was submitted or round started)
            }
            finally
            {
                cts.Dispose();
            }
        });
    }
}

