using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Services;

namespace PhpbbInDotnet.Forum.Pages;

[ValidateAntiForgeryToken]
public class BotChallengeModel(IBotDetectionService botDetectionService) : PageModel
{
    [BindProperty(Name = "cf-turnstile-response")]
    public string? BotDetectorResponse { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Target { get; set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPost()
    {
        if (await botDetectionService.IsBot(BotDetectorResponse, HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty))
        {
            return Forbid();
        }

        return Redirect(HttpUtility.UrlDecode(Target!));
    }
}