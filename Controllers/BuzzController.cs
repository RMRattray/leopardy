using Microsoft.AspNetCore.Mvc;
using Leopardy.Services;

namespace Leopardy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BuzzController : ControllerBase
{
    private readonly GameManager _gameManager;

    public BuzzController(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    [HttpPost("{gameId}")]
    public IActionResult BuzzIn(string gameId, [FromBody] BuzzRequest request)
    {
        // Get connection ID from request (in a real app, you'd get this from the SignalR context)
        // For now, we'll use a simplified approach where the connection ID is passed
        // In practice, you'd want to map connection IDs to player names or use SignalR directly
        
        var success = _gameManager.BuzzIn(gameId, request.ConnectionId);
        
        if (success)
        {
            var game = _gameManager.GetGame(gameId);
            if (game?.CurrentPlayer != null)
            {
                return Ok(new { 
                    success = true, 
                    playerName = game.CurrentPlayer.Name,
                    connectionId = game.CurrentPlayer.ConnectionId
                });
            }
        }
        
        return BadRequest(new { success = false, message = "Unable to buzz in" });
    }
}

public class BuzzRequest
{
    public string ConnectionId { get; set; } = string.Empty;
}

