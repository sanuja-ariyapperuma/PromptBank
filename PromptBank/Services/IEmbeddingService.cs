namespace PromptBank.Services;

/// <summary>
/// Provides text embedding computation for semantic search.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a byte-serialized embedding vector for the given text.
    /// </summary>
    /// <param name="text">The input text to embed.</param>
    /// <returns>A byte array representing the embedding vector.</returns>
    byte[] GetEmbeddingBytes(string text);

    /// <summary>
    /// Computes cosine similarity between two serialized embedding byte arrays.
    /// </summary>
    /// <param name="a">First embedding bytes (from <see cref="GetEmbeddingBytes"/>).</param>
    /// <param name="b">Second embedding bytes (from <see cref="GetEmbeddingBytes"/>).</param>
    /// <returns>A similarity score in approximately [-1, 1]; higher values indicate more similar text.</returns>
    float Similarity(byte[] a, byte[] b);
}
