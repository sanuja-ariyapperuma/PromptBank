using Microsoft.EntityFrameworkCore;
using PromptBank.Data;

namespace PromptBank.Services;

/// <summary>
/// Hosted background service that generates missing <see cref="Models.Prompt.TitleDescriptionEmbedding"/>
/// values after the application has finished starting.
/// Running this as a <see cref="BackgroundService"/> keeps it off the critical startup path so
/// Azure App Service health checks succeed before the (slow) ONNX model is loaded.
/// </summary>
public class EmbeddingBackfillService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<EmbeddingBackfillService> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="EmbeddingBackfillService"/>.
    /// </summary>
    /// <param name="services">Root service provider used to create a scoped DB context.</param>
    /// <param name="logger">Logger.</param>
    public EmbeddingBackfillService(IServiceProvider services, ILogger<EmbeddingBackfillService> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Runs the embedding backfill once after startup then exits.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token provided by the host.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately so the host can finish starting and serve HTTP requests
        // before we start loading the (slow) ONNX model.
        await Task.Yield();

        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

            var needsEmbedding = await db.Prompts
                .Where(p => p.TitleDescriptionEmbedding == null)
                .ToListAsync(stoppingToken);

            if (needsEmbedding.Count == 0)
            {
                _logger.LogInformation("EmbeddingBackfill: all prompts already have embeddings.");
                return;
            }

            _logger.LogInformation("EmbeddingBackfill: generating embeddings for {Count} prompt(s).", needsEmbedding.Count);

            foreach (var prompt in needsEmbedding)
            {
                stoppingToken.ThrowIfCancellationRequested();
                prompt.TitleDescriptionEmbedding = embeddingService.GetEmbeddingBytes($"{prompt.Title} {prompt.Description}");
            }

            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("EmbeddingBackfill: completed successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("EmbeddingBackfill: cancelled before completion.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmbeddingBackfill: failed with an unexpected error.");
        }
    }
}
