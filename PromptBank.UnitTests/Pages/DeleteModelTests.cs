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
/// Unit tests for <see cref="DeleteModel"/>.
/// <see cref="IPromptService"/> is mocked with Moq so that every handler is
/// exercised without a database or HTTP pipeline.
/// </summary>
public class DeleteModelTests
{
    private const string UserId = "user-1";

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constructs a <see cref="DeleteModel"/> wired to the supplied mock service
    /// and initialised with an authenticated <see cref="PageContext"/>.
    /// </summary>
    private static DeleteModel CreatePageModel(Mock<IPromptService> mockService, int id = 1)
    {
        var model = new DeleteModel(mockService.Object) { Id = id };
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
        Title = "Prompt to Delete",
        Content = "Some content",
        OwnerId = UserId
    };

    // -----------------------------------------------------------------------
    // OnGetAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="DeleteModel.OnGetAsync"/> sets the
    /// <see cref="DeleteModel.Prompt"/> property and returns a
    /// <see cref="PageResult"/> when the requested prompt exists and is owned by the user.
    /// </summary>
    [Fact]
    public async Task OnGetAsync_SetsPromptProperty_WhenFound()
    {
        var prompt = MakePrompt();
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prompt);

        var model = CreatePageModel(mockService, id: 1);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Prompt);
        Assert.Equal(1, model.Prompt.Id);
        Assert.Equal("Prompt to Delete", model.Prompt.Title);
    }

    /// <summary>
    /// Verifies that <see cref="DeleteModel.OnGetAsync"/> returns a
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
        Assert.Null(model.Prompt);
    }

    // -----------------------------------------------------------------------
    // OnPostAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="DeleteModel.OnPostAsync"/> calls
    /// <see cref="IPromptService.DeleteAsync"/> with the correct id and user, and redirects
    /// to <c>/Index</c> on success.
    /// </summary>
    [Fact]
    public async Task OnPostAsync_RedirectsToIndex_WhenDeleted()
    {
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.DeleteAsync(1, UserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var model = CreatePageModel(mockService, id: 1);

        var result = await model.OnPostAsync();

        mockService.Verify(s => s.DeleteAsync(1, UserId, It.IsAny<CancellationToken>()), Times.Once);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
    }

    /// <summary>
    /// Verifies that <see cref="DeleteModel.OnPostAsync"/> returns 404 Not Found
    /// when the service throws <see cref="KeyNotFoundException"/>.
    /// </summary>
    [Fact]
    public async Task OnPostAsync_ReturnsNotFound_WhenKeyNotFound()
    {
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Prompt not found."));

        var model = CreatePageModel(mockService, id: 999);

        var result = await model.OnPostAsync();

        Assert.IsType<NotFoundResult>(result);
    }
}
