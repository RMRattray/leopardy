using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Leopardy.Data;
using Leopardy.Models;
using Leopardy.Services;
using System.Text.Json;

namespace Leopardy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CsvService _csvService;

    public GamesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        CsvService csvService)
    {
        _context = context;
        _userManager = userManager;
        _csvService = csvService;
    }

    [HttpPost("export-csv")]
    public IActionResult ExportCsv([FromBody] GameDataDto gameData)
    {
        if (gameData?.Categories == null || gameData.Categories.Count == 0)
        {
            return BadRequest("No categories provided");
        }

        var categories = gameData.Categories.Select(c => new Category
        {
            Name = c.Name,
            Clues = c.Clues.Select(cl => new Clue
            {
                Question = cl.Question,
                Answer = cl.Answer,
                Value = cl.Value
            }).ToList()
        }).ToList();

        var csv = _csvService.ExportToCsv(categories);
        return Content(csv, "text/csv");
    }

    [HttpPost("import-csv")]
    public IActionResult ImportCsv([FromBody] CsvImportDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CsvContent))
        {
            return BadRequest("No CSV content provided");
        }

        try
        {
            var categories = _csvService.ImportFromCsv(dto.CsvContent);
            
            if (categories.Count == 0)
            {
                return BadRequest("No valid categories found in CSV");
            }

            return Ok(new { categories });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error parsing CSV: {ex.Message}");
        }
    }

    [Authorize]
    [HttpPost("save")]
    public async Task<IActionResult> SaveGame([FromBody] GameDataDto gameData)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(gameData.Name))
        {
            return BadRequest("Game name is required");
        }

        if (gameData.Categories == null || gameData.Categories.Count == 0)
        {
            return BadRequest("At least one category is required");
        }

        var categories = gameData.Categories.Select(c => new Category
        {
            Name = c.Name,
            Clues = c.Clues.Select(cl => new Clue
            {
                Question = cl.Question,
                Answer = cl.Answer,
                Value = cl.Value
            }).ToList()
        }).ToList();

        var gameJson = JsonSerializer.Serialize(categories);

        var savedGame = new SavedGame
        {
            Name = gameData.Name,
            UserId = user.Id,
            GameData = gameJson,
            CreatedAt = DateTime.UtcNow
        };

        _context.SavedGames.Add(savedGame);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, id = savedGame.Id });
    }

    [Authorize]
    [HttpGet("saved")]
    public async Task<IActionResult> GetSavedGames()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var games = await _context.SavedGames
            .Where(g => g.UserId == user.Id)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new
            {
                id = g.Id,
                name = g.Name,
                createdAt = g.CreatedAt
            })
            .ToListAsync();

        return Ok(games);
    }

    [Authorize]
    [HttpGet("saved/{id}")]
    public async Task<IActionResult> GetSavedGame(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var game = await _context.SavedGames
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == user.Id);

        if (game == null)
        {
            return NotFound();
        }

        var categories = JsonSerializer.Deserialize<List<Category>>(game.GameData);
        
        return Ok(new
        {
            id = game.Id,
            name = game.Name,
            categories = categories
        });
    }

    [Authorize]
    [HttpDelete("saved/{id}")]
    public async Task<IActionResult> DeleteSavedGame(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var game = await _context.SavedGames
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == user.Id);

        if (game == null)
        {
            return NotFound();
        }

        _context.SavedGames.Remove(game);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }
}

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpGet("check")]
    public IActionResult CheckAuth()
    {
        return Ok(new { isAuthenticated = User.Identity?.IsAuthenticated ?? false });
    }
}

public class GameDataDto
{
    public string Name { get; set; } = string.Empty;
    public List<CategoryDto> Categories { get; set; } = new();
}

public class CategoryDto
{
    public string Name { get; set; } = string.Empty;
    public List<ClueDto> Clues { get; set; } = new();
}

public class ClueDto
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class CsvImportDto
{
    public string CsvContent { get; set; } = string.Empty;
}

