using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Domain.Extensions;
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
        if (await botDetectionService.IsBot(BotDetectorResponse, HttpContext.GetIpAddress() ?? string.Empty))
        {
            return Forbid();
        }

        if (!Url.IsLocalUrl(Target))
        {
            return BadRequest();
        }

        return LocalRedirect(Target);
    }
}