using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using PromptBank.Models;
using PromptBank.Services;

namespace PromptBank.Pages.Prompts;

/// <summary>
/// Page model for deleting a prompt, with a confirmation step.
/// </summary>
[Authorize]
public class DeleteModel : PageModel
{
    private readonly IPromptService _promptService;

    /// <summary>
    /// Initialises a new instance of <see cref="DeleteModel"/>.
    /// </summary>
    /// <param name="promptService">The injected prompt service.</param>
    public DeleteModel(IPromptService promptService)
    {
        _promptService = promptService;
    }

    /// <summary>Gets or sets the ID of the prompt to be deleted.</summary>
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    /// <summary>Gets the prompt loaded for the confirmation view.</summary>
    public Prompt? Prompt { get; private set; }

    /// <summary>
    /// Loads the prompt to display in the confirmation page.
    /// </summary>
    /// <returns>The confirmation page, or 404 if the prompt does not exist, or 403 if not the owner.</returns>
    public async Task<IActionResult> OnGetAsync()
    {
        Prompt = await _promptService.GetByIdAsync(Id);
        if (Prompt is null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Prompt.OwnerId != currentUserId)
            return Forbid();

        return Page();
    }

    /// <summary>
    /// Handles the confirmed deletion and redirects to the listing.
    /// </summary>
    /// <returns>Redirects to Index on success; 404 if the prompt was already removed.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            await _promptService.DeleteAsync(Id, userId);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToPage("/Index");
    }
}
