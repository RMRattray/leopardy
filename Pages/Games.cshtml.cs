using Leopardy.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Leopardy.Pages;

public class GamesModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public GamesModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool IsAuthenticated { get; private set; }
    public int? HighlightSavedGameId { get; private set; }
    public List<SavedGameListItem> SavedGames { get; private set; } = new();

    public async Task OnGetAsync(int? savedGameId)
    {
        IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
        HighlightSavedGameId = savedGameId;

        if (!IsAuthenticated)
        {
            return;
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        SavedGames = await _context.SavedGames
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new SavedGameListItem
            {
                Id = g.Id,
                Name = g.Name,
                CreatedAt = g.CreatedAt
            })
            .ToListAsync();
    }
}

public class SavedGameListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
