using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using PromptBank.Models;

namespace PromptBank.Pages.Account;

/// <summary>
/// Page model for the registration page.
/// </summary>
public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    /// <summary>
    /// Initialises a new instance of <see cref="RegisterModel"/>.
    /// </summary>
    /// <param name="userManager">The injected user manager.</param>
    /// <param name="signInManager">The injected sign-in manager.</param>
    public RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    /// <summary>Gets or sets the registration input bound from the form.</summary>
    [BindProperty]
    public RegisterInputModel Input { get; set; } = new();

    /// <summary>Displays the registration form.</summary>
    /// <returns>The page result.</returns>
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Index");
        return Page();
    }

    /// <summary>
    /// Handles form submission, creates the user account, and signs them in.
    /// </summary>
    /// <returns>Redirects to Index on success; returns the form with errors on failure.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = new ApplicationUser
        {
            UserName = Input.Username,
            Email = $"{Input.Username}@promptbank.local"
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToPage("/Index");
    }
}

/// <summary>
/// Input model for the registration form.
/// </summary>
public class RegisterInputModel
{
    /// <summary>Gets or sets the desired username.</summary>
    [Required]
    [MaxLength(50)]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the password.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the password confirmation.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
