using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PromptBank.Models;

namespace PromptBank.Pages.Account;

/// <summary>
/// Page model for signing the user out of the application.
/// </summary>
public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    /// <summary>
    /// Initialises a new instance of <see cref="LogoutModel"/>.
    /// </summary>
    /// <param name="signInManager">The injected sign-in manager.</param>
    public LogoutModel(SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }

    /// <summary>
    /// Signs the user out and redirects to the home page.
    /// </summary>
    /// <returns>Redirect to the Index page.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        await _signInManager.SignOutAsync();
        return RedirectToPage("/Index");
    }
}
