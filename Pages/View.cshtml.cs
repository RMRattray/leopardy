using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Leopardy.Pages;

public class ViewModel : PageModel
{
    public string? GameId { get; set; }

    public void OnGet(string? gameId)
    {
        GameId = gameId;
    }
}

