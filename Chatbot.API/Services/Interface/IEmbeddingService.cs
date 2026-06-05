namespace Chatbot.API.Services.Interface
{
    public interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text);
        float CosineSimilarity(float[] a, float[] b);
    }
}