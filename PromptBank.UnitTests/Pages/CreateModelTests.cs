using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Moq;
using PromptBank.Models;
using PromptBank.Pages.Prompts;
using PromptBank.Services;

namespace PromptBank.UnitTests.Pages;

/// <summary>
/// Unit tests for <see cref="CreateModel"/>.
/// <see cref="IPromptService"/> is mocked with Moq so tests run without
/// any database or HTTP pipeline dependencies.
/// </summary>
public class CreateModelTests
{
    private const string UserId = "user-1";

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constructs a <see cref="CreateModel"/> wired to the supplied mock service
    /// and initialised with an authenticated <see cref="PageContext"/>.
    /// </summary>
    private static CreateModel CreatePageModel(Mock<IPromptService> mockService)
    {
        var model = new CreateModel(mockService.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, UserId) }, "Test"));
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new PageActionDescriptor(),
            modelState);
        model.PageContext = new PageContext(actionContext);
        return model;
    }

    /// <summary>Returns a fully populated, valid <see cref="PromptInputModel"/>.</summary>
    private static PromptInputModel ValidInput() => new()
    {
        Title = "My Prompt",
        Content = "Explain this code."
    };

    // -----------------------------------------------------------------------
    // OnGet
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="CreateModel.OnGet"/> returns a <see cref="PageResult"/>
    /// to render the empty create form.
    /// </summary>
    [Fact]
    public void OnGet_ReturnsPageResult()
    {
        var mockService = new Mock<IPromptService>();
        var model = CreatePageModel(mockService);

        var result = model.OnGet();

        Assert.IsType<PageResult>(result);
    }

    // -----------------------------------------------------------------------
    // OnPostAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="CreateModel.OnPostAsync"/> calls
    /// <see cref="IPromptService.CreateAsync"/> and redirects to <c>/Index</c>
    /// when model state is valid.
    /// </summary>
    [Fact]
    public async Task OnPostAsync_RedirectsToIndex_WhenValid()
    {
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.CreateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prompt p, CancellationToken _) => p);

        var model = CreatePageModel(mockService);
        model.Input = ValidInput();

        var result = await model.OnPostAsync();

        mockService.Verify(
            s => s.CreateAsync(
                It.Is<Prompt>(p =>
                    p.Title == "My Prompt" &&
                    p.Content == "Explain this code." &&
                    p.OwnerId == UserId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
    }

    /// <summary>
    /// Verifies that <see cref="CreateModel.OnPostAsync"/> returns the create
    /// <see cref="PageResult"/> (displaying validation errors) when
    /// <see cref="PageModel.ModelState"/> is invalid, and does <em>not</em> call
    /// the service.
    /// </summary>
    [Fact]
    public async Task OnPostAsync_ReturnsPage_WhenModelStateInvalid()
    {
        var mockService = new Mock<IPromptService>();
        var model = CreatePageModel(mockService);
        model.ModelState.AddModelError("Input.Title", "The Title field is required.");

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        mockService.Verify(
            s => s.CreateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
