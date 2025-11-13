using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Leopardy.Models;

namespace Leopardy.Data;

public class ApplicationUser : IdentityUser
{
    public List<SavedGame> SavedGames { get; set; } = new();
}

public class SavedGame
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string GameData { get; set; } = string.Empty; // JSON serialized categories
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<SavedGame> SavedGames { get; set; }
}

