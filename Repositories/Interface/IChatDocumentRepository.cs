using Chatbot.API.Core.Models;

namespace Chatbot.API.Repositories.Interface
{
    public interface IChatDocumentRepository
    {
        Task<IEnumerable<ChatDocument>> GetAllAsync();
        Task<IEnumerable<ChatDocument>> GetAllWithEmbeddingsAsync();
        Task<IEnumerable<ChatDocument>> GetByCategoryAsync(string category);
        Task<IEnumerable<ChatDocument>> SearchAsync(string query);
        Task<DateTime?> GetLastScrapedDateAsync();
        Task<IEnumerable<string>> GetScrapedUrlsAsync();
        Task AddAsync(ChatDocument document);
        Task AddRangeAsync(IEnumerable<ChatDocument> documents);
        Task DeleteAllAsync();
        Task DeleteByUrlAsync(string url);
        Task SaveChangesAsync();
    }
}