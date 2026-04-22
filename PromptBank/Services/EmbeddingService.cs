using SmartComponents.LocalEmbeddings;

namespace PromptBank.Services;

/// <summary>
/// Wraps <see cref="LocalEmbedder"/> to generate and compare text embeddings using a
/// locally-bundled ONNX sentence-transformer model (no external API required).
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly LocalEmbedder _embedder;

    /// <summary>
    /// Initialises a new instance of <see cref="EmbeddingService"/>.
    /// </summary>
    /// <param name="embedder">The injected <see cref="LocalEmbedder"/> singleton.</param>
    public EmbeddingService(LocalEmbedder embedder)
    {
        _embedder = embedder;
    }

    /// <inheritdoc />
    public byte[] GetEmbeddingBytes(string text)
        => _embedder.Embed(text).Buffer.ToArray();

    /// <inheritdoc />
    public float Similarity(byte[] a, byte[] b)
    {
        var ea = new EmbeddingF32(a.AsMemory());
        var eb = new EmbeddingF32(b.AsMemory());
        return ea.Similarity(eb);
    }
}
