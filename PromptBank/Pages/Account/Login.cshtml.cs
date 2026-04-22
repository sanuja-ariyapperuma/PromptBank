using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using PromptBank.Models;

namespace PromptBank.Pages.Account;

/// <summary>
/// Page model for the login page.
/// </summary>
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    /// <summary>
    /// Initialises a new instance of <see cref="LoginModel"/>.
    /// </summary>
    /// <param name="signInManager">The injected sign-in manager.</param>
    public LoginModel(SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }

    /// <summary>Gets or sets the login input bound from the form.</summary>
    [BindProperty]
    public LoginInputModel Input { get; set; } = new();

    /// <summary>Displays the login form.</summary>
    /// <returns>The page result.</returns>
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Index");
        return Page();
    }

    /// <summary>
    /// Handles form submission, validates credentials, and signs the user in.
    /// </summary>
    /// <returns>Redirects to Index on success; returns the form with errors on failure.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await _signInManager.PasswordSignInAsync(
            Input.Username, Input.Password, Input.RememberMe, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        return RedirectToPage("/Index");
    }
}

/// <summary>
/// Input model for the login form.
/// </summary>
public class LoginInputModel
{
    /// <summary>Gets or sets the username.</summary>
    [Required]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the password.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether to persist the login cookie.</summary>
    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}
