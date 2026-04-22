using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Moq;
using PromptBank.Models;
using PromptBank.Pages;
using PromptBank.Services;

namespace PromptBank.UnitTests.Pages;

/// <summary>
/// Unit tests for <see cref="IndexModel"/>.
/// <see cref="IPromptService"/> is mocked with Moq so that page handler logic
/// is tested in complete isolation from the database.
/// </summary>
public class IndexModelTests
{
    private const string UserId = "user-1";

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constructs an <see cref="IndexModel"/> with an anonymous user context.
    /// </summary>
    private static IndexModel CreateModel(Mock<IPromptService> mockService)
    {
        var model = new IndexModel(mockService.Object);
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new PageActionDescriptor(),
            modelState);
        model.PageContext = new PageContext(actionContext);
        return model;
    }

    /// <summary>
    /// Constructs an <see cref="IndexModel"/> with an authenticated user context.
    /// </summary>
    private static IndexModel CreateAuthenticatedModel(Mock<IPromptService> mockService)
    {
        var model = new IndexModel(mockService.Object);
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

    // -----------------------------------------------------------------------
    // OnGetAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="IndexModel.OnGetAsync"/> populates
    /// <see cref="IndexModel.Prompts"/> with the list returned by the service.
    /// </summary>
    [Fact]
    public async Task OnGetAsync_PopulatesPrompts_FromService()
    {
        var prompts = new List<Prompt>
        {
            new() { Id = 1, Title = "A", Content = "C", OwnerId = UserId },
            new() { Id = 2, Title = "B", Content = "C", OwnerId = UserId }
        };

        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.GetAllAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prompts);

        var model = CreateModel(mockService);

        await model.OnGetAsync();

        Assert.Equal(2, model.Prompts.Count);
        Assert.Equal("A", model.Prompts[0].Title);
        Assert.Equal("B", model.Prompts[1].Title);
    }

    // -----------------------------------------------------------------------
    // OnPostRateAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="IndexModel.OnPostRateAsync"/> returns a
    /// <see cref="JsonResult"/> containing the updated <see cref="RatingResult"/>
    /// when the model state is valid and the service call succeeds.
    /// </summary>
    [Fact]
    public async Task OnPostRateAsync_ReturnsJsonResult_WhenValid()
    {
        var expected = new RatingResult(4.5, 10);
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.RateAsync(1, UserId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var model = CreateAuthenticatedModel(mockService);

        var result = await model.OnPostRateAsync(new RateRequest(1, 5));

        var jsonResult = Assert.IsType<JsonResult>(result);
        var ratingResult = Assert.IsType<RatingResult>(jsonResult.Value);
        Assert.Equal(4.5, ratingResult.Average);
        Assert.Equal(10, ratingResult.Count);
    }

    /// <summary>
    /// Verifies that <see cref="IndexModel.OnPostRateAsync"/> returns 401 Unauthorized
    /// when the user is not authenticated.
    /// </summary>
    [Fact]
    public async Task OnPostRateAsync_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var mockService = new Mock<IPromptService>();
        var model = CreateModel(mockService);

        var result = await model.OnPostRateAsync(new RateRequest(1, 5));

        Assert.IsType<UnauthorizedResult>(result);
    }

    /// <summary>
    /// Verifies that <see cref="IndexModel.OnPostRateAsync"/> returns a 400 Bad Request
    /// when <see cref="PageModel.ModelState"/> contains validation errors.
    /// </summary>
    [Fact]
    public async Task OnPostRateAsync_ReturnsBadRequest_WhenModelStateInvalid()
    {
        var mockService = new Mock<IPromptService>();
        var model = CreateAuthenticatedModel(mockService);
        model.ModelState.AddModelError("Stars", "Stars must be between 1 and 5.");

        var result = await model.OnPostRateAsync(new RateRequest(1, 0));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Verifies that <see cref="IndexModel.OnPostRateAsync"/> returns 404 Not Found
    /// when the service throws <see cref="KeyNotFoundException"/>.
    /// </summary>
    [Fact]
    public async Task OnPostRateAsync_ReturnsNotFound_WhenKeyNotFound()
    {
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.RateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Prompt not found."));

        var model = CreateAuthenticatedModel(mockService);

        var result = await model.OnPostRateAsync(new RateRequest(999, 3));

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// Verifies that <see cref="IndexModel.OnPostRateAsync"/> returns 400 Bad Request
    /// when the service throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public async Task OnPostRateAsync_ReturnsBadRequest_WhenArgumentOutOfRange()
    {
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.RateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentOutOfRangeException("stars", "Stars must be between 1 and 5."));

        var model = CreateAuthenticatedModel(mockService);

        var result = await model.OnPostRateAsync(new RateRequest(1, 6));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // -----------------------------------------------------------------------
    // OnPostTogglePinAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="IndexModel.OnPostTogglePinAsync"/> returns a
    /// <see cref="JsonResult"/> with an <c>isPinned</c> property when the service
    /// call succeeds.
    /// </summary>
    [Fact]
    public async Task OnPostTogglePinAsync_ReturnsJsonResult_WhenValid()
    {
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.TogglePinAsync(1, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var model = CreateAuthenticatedModel(mockService);

        var result = await model.OnPostTogglePinAsync(new TogglePinRequest(1));

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);

        var isPinnedProp = jsonResult.Value!.GetType().GetProperty("isPinned");
        Assert.NotNull(isPinnedProp);
        Assert.Equal(true, isPinnedProp.GetValue(jsonResult.Value));
    }

    /// <summary>
    /// Verifies that <see cref="IndexModel.OnPostTogglePinAsync"/> returns 401 Unauthorized
    /// when the user is not authenticated.
    /// </summary>
    [Fact]
    public async Task OnPostTogglePinAsync_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var mockService = new Mock<IPromptService>();
        var model = CreateModel(mockService);

        var result = await model.OnPostTogglePinAsync(new TogglePinRequest(1));

        Assert.IsType<UnauthorizedResult>(result);
    }

    /// <summary>
    /// Verifies that <see cref="IndexModel.OnPostTogglePinAsync"/> returns 404 Not Found
    /// when the service throws <see cref="KeyNotFoundException"/>.
    /// </summary>
    [Fact]
    public async Task OnPostTogglePinAsync_ReturnsNotFound_WhenKeyNotFound()
    {
        var mockService = new Mock<IPromptService>();
        mockService
            .Setup(s => s.TogglePinAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Prompt not found."));

        var model = CreateAuthenticatedModel(mockService);

        var result = await model.OnPostTogglePinAsync(new TogglePinRequest(999));

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// Verifies that <see cref="IndexModel.OnPostTogglePinAsync"/> returns 400 Bad Request
    /// when <see cref="PageModel.ModelState"/> contains validation errors,
    /// and does <em>not</em> call the service.
    /// </summary>
    [Fact]
    public async Task OnPostTogglePinAsync_ReturnsBadRequest_WhenModelStateInvalid()
    {
        var mockService = new Mock<IPromptService>();
        var model = CreateAuthenticatedModel(mockService);
        model.ModelState.AddModelError("Id", "The Id field is required.");

        var result = await model.OnPostTogglePinAsync(new TogglePinRequest(0));

        Assert.IsType<BadRequestObjectResult>(result);
        mockService.Verify(
            s => s.TogglePinAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
