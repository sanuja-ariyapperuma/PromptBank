using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using PromptBank.Services;

namespace PromptBank.Pages.Prompts;

/// <summary>
/// Page model for editing an existing prompt.
/// </summary>
[Authorize]
public class EditModel : PageModel
{
    private readonly IPromptService _promptService;

    /// <summary>
    /// Initialises a new instance of <see cref="EditModel"/>.
    /// </summary>
    /// <param name="promptService">The injected prompt service.</param>
    public EditModel(IPromptService promptService)
    {
        _promptService = promptService;
    }

    /// <summary>Gets or sets the ID of the prompt being edited.</summary>
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    /// <summary>Gets or sets the form input bound from the edit form.</summary>
    [BindProperty]
    public PromptInputModel Input { get; set; } = new();

    /// <summary>
    /// Loads the existing prompt into the form fields.
    /// </summary>
    /// <returns>The edit page, or 404 if the prompt does not exist, or 403 if not the owner.</returns>
    public async Task<IActionResult> OnGetAsync()
    {
        var prompt = await _promptService.GetByIdAsync(Id);
        if (prompt is null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (prompt.OwnerId != currentUserId)
            return Forbid();

        Input = new PromptInputModel
        {
            Title = prompt.Title,
            Description = prompt.Description,
            Content = prompt.Content
        };

        return Page();
    }

    /// <summary>
    /// Handles form submission and persists the updated prompt.
    /// </summary>
    /// <returns>Redirects to Index on success; returns the form with errors on failure.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            await _promptService.UpdateAsync(Id, userId, Input.Title, Input.Description, Input.Content);
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
