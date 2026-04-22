using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using PromptBank.Models;
using PromptBank.Services;

namespace PromptBank.Pages.Prompts;

/// <summary>
/// Page model for adding a new prompt to the Prompt Bank.
/// </summary>
[Authorize]
public class CreateModel : PageModel
{
    private readonly IPromptService _promptService;

    /// <summary>
    /// Initialises a new instance of <see cref="CreateModel"/>.
    /// </summary>
    /// <param name="promptService">The injected prompt service.</param>
    public CreateModel(IPromptService promptService)
    {
        _promptService = promptService;
    }

    /// <summary>Gets or sets the prompt being created, bound from the form.</summary>
    [BindProperty]
    public PromptInputModel Input { get; set; } = new();

    /// <summary>Displays the create form.</summary>
    /// <returns>The page result.</returns>
    public IActionResult OnGet() => Page();

    /// <summary>
    /// Handles form submission, validates input, and persists the new prompt.
    /// </summary>
    /// <returns>Redirects to Index on success; returns the form with errors on failure.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        await _promptService.CreateAsync(new Prompt
        {
            Title = Input.Title,
            Description = Input.Description,
            Content = Input.Content,
            OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!
        });

        return RedirectToPage("/Index");
    }
}

/// <summary>
/// Input model for creating a new prompt, with validation rules applied.
/// </summary>
public class PromptInputModel
{
    /// <summary>Gets or sets the prompt title.</summary>
    [Required]
    [MaxLength(200)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the description of the prompt's purpose.</summary>
    [Required]
    [MaxLength(500)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the full prompt text.</summary>
    [Required]
    [MaxLength(4000)]
    [Display(Name = "Prompt Content")]
    public string Content { get; set; } = string.Empty;
}
