using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using PromptBank.Models;
using PromptBank.Services;
using System.Security.Claims;

namespace PromptBank.Pages;

/// <summary>
/// Page model for the prompt listing page.
/// Handles the main list view plus AJAX handlers for rating and pin toggling.
/// </summary>
public class IndexModel : PageModel
{
    private readonly IPromptService _promptService;

    /// <summary>
    /// Initialises a new instance of <see cref="IndexModel"/>.
    /// </summary>
    /// <param name="promptService">The injected prompt service.</param>
    public IndexModel(IPromptService promptService)
    {
        _promptService = promptService;
    }

    /// <summary>Gets the sorted list of prompts to display.</summary>
    public IReadOnlyList<Prompt> Prompts { get; private set; } = [];

    /// <summary>Gets the current authenticated user's ID, or null if anonymous.</summary>
    public string? CurrentUserId { get; private set; }

    /// <summary>Gets the set of prompt IDs pinned by the current user.</summary>
    public HashSet<int> UserPinnedIds { get; private set; } = [];

    /// <summary>Gets or sets the current search query (bound from the <c>q</c> query-string parameter).</summary>
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? SearchQuery { get; set; }

    /// <summary>Loads all prompts, or filters semantically if a search query is present.</summary>
    public async Task OnGetAsync()
    {
        CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
            Prompts = await _promptService.SearchAsync(SearchQuery.Trim(), CurrentUserId);
        else
            Prompts = await _promptService.GetAllAsync(CurrentUserId);

        if (CurrentUserId != null)
        {
            UserPinnedIds = Prompts
                .Where(p => p.Pins.Any(pin => pin.UserId == CurrentUserId))
                .Select(p => p.Id)
                .ToHashSet();
        }
    }

    /// <summary>
    /// AJAX handler: records a star rating vote and returns the updated aggregate.
    /// </summary>
    /// <param name="request">The rating request containing the prompt ID and star value (1–5).</param>
    /// <returns>JSON with <c>average</c> and <c>count</c> fields, or appropriate error status.</returns>
    [EnableRateLimiting("ajax")]
    public async Task<IActionResult> OnPostRateAsync([FromBody] RateRequest request)
    {
        if (!User.Identity!.IsAuthenticated)
            return Unauthorized();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            var result = await _promptService.RateAsync(request.Id, userId, request.Stars);
            if (result is null)
                return StatusCode(409, "Already rated");
            return new JsonResult(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// AJAX handler: toggles the pinned state of a prompt for the current user.
    /// </summary>
    /// <param name="request">The request containing the prompt ID to toggle.</param>
    /// <returns>JSON with an <c>isPinned</c> boolean field.</returns>
    [EnableRateLimiting("ajax")]
    public async Task<IActionResult> OnPostTogglePinAsync([FromBody] TogglePinRequest request)
    {
        if (!User.Identity!.IsAuthenticated)
            return Unauthorized();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            var isPinned = await _promptService.TogglePinAsync(request.Id, userId);
            return new JsonResult(new { isPinned });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
