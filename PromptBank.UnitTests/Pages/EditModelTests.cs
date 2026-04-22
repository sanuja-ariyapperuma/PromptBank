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
/// Unit tests for <see cref="EditModel"/>.
/// <see cref="IPromptService"/> is mocked with Moq so that every handler is
/// exercised without a database or HTTP pipeline.
/// </summary>
public class EditModelTests
{
    private const string UserId = "user-1";

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constructs an <see cref="EditModel"/> wired to the supplied mock service
    /// and initialised with an authenticated <see cref="PageContext"/>.
    /// </summary>
    private static EditModel CreatePageModel(Mock<IPromptService> mockService, int id = 1)
    {
        var model = new EditModel(mockService.Object) { Id = id };
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

    /// <summary>Creates a minimal valid <see cref="Prompt"/> with the given id, owned by the test user.</summary>
    private static Prompt MakePrompt(int id = 1) => new()
    {
        Id = id,
        Title = "Original Title",
        Content = "Original Content",
        OwnerId = UserId
    };

    /// <summary>Returns a valid <see cref="PromptInputModel"/>.</summary>
    private static PromptInputModel ValidInput() => new()
    {
        Title = "Updated Title",
        Description = "Updated Description",
        Content = "Updated Content"
    };

    // -----------------------------------------------------------------------
    // OnGetAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="EditModel.OnGetAsync"/> populates
    /// <see cref="EditModel.Input"/> from the retrieved prompt and returns a
    /// <see cref="PageResult"/> when the prompt exists and is owned by the user.
    /// </summary>
    [Fact]
    public async Task OnGetAsync_PopulatesInput_WhenPromptFound()
    {
        var prompt = MakePrompt();
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prompt);

        var model = CreatePageModel(mockService, id: 1);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Original Title", model.Input.Title);
        Assert.Equal("Original Content", model.Input.Content);
    }

    /// <summary>
    /// Verifies that <see cref="EditModel.OnGetAsync"/> returns a
    /// <see cref="NotFoundResult"/> when the requested prompt does not exist.
    /// </summary>
    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenPromptMissing()
    {
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prompt?)null);

        var model = CreatePageModel(mockService, id: 999);

        var result = await model.OnGetAsync();

        Assert.IsType<NotFoundResult>(result);
    }

    // -----------------------------------------------------------------------
    // OnPostAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="EditModel.OnPostAsync"/> calls
    /// <see cref="IPromptService.UpdateAsync"/> with the correct arguments and
    /// redirects to <c>/Index</c> when model state is valid.
    /// </summary>
    [Fact]
    public async Task OnPostAsync_RedirectsToIndex_WhenValid()
    {
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.UpdateAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var model = CreatePageModel(mockService, id: 1);
        model.Input = ValidInput();

        var result = await model.OnPostAsync();

        mockService.Verify(
            s => s.UpdateAsync(
                1,
                UserId,
                "Updated Title",
                "Updated Description",
                "Updated Content",
                It.IsAny<CancellationToken>()),
            Times.Once);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
    }

    /// <summary>
    /// Verifies that <see cref="EditModel.OnPostAsync"/> returns the edit
    /// <see cref="PageResult"/> when <see cref="PageModel.ModelState"/> is invalid,
    /// and does <em>not</em> call the service.
    /// </summary>
    [Fact]
    public async Task OnPostAsync_ReturnsPage_WhenModelStateInvalid()
    {
        var mockService = new Mock<IPromptService>();
        var model = CreatePageModel(mockService, id: 1);
        model.ModelState.AddModelError("Input.Title", "The Title field is required.");

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        mockService.Verify(
            s => s.UpdateAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that <see cref="EditModel.OnPostAsync"/> returns 404 Not Found
    /// when the service throws <see cref="KeyNotFoundException"/>.
    /// </summary>
    [Fact]
    public async Task OnPostAsync_ReturnsNotFound_WhenKeyNotFound()
    {
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.UpdateAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Prompt not found."));

        var model = CreatePageModel(mockService, id: 999);
        model.Input = ValidInput();

        var result = await model.OnPostAsync();

        Assert.IsType<NotFoundResult>(result);
    }
}
