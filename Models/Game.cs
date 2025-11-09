namespace Leopardy.Models;

public class Game
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string HostConnectionId { get; set; } = string.Empty;
    public bool Started { get; set; } = false;
    public List<Player> Players { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public Clue? CurrentClue { get; set; }
    public string? CurrentCategory { get; set; }
    public int? CurrentValue { get; set; }
    public Player? CurrentPlayer { get; set; }
    public string? CurrentAnswer { get; set; }
    public bool ClueRevealed { get; set; }
    public bool WaitingForAnswer { get; set; }
    public DateTime? BuzzTime { get; set; }
    public Dictionary<string, bool> ClueAnswered { get; set; } = new();
}

public class Player
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool HasControl { get; set; }
    public DateTime? LastBuzzTime { get; set; }
}

public class Category
{
    public string Name { get; set; } = string.Empty;
    public List<Clue> Clues { get; set; } = new();
}

public class Clue
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int Value { get; set; }
    public bool IsDailyDouble { get; set; }
}

