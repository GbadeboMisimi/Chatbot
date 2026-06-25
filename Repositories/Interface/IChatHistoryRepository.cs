using Chatbot.API.Core.Models;

namespace Chatbot.API.Repositories.Interface
{
    public interface IChatHistoryRepository
    {
        Task<IEnumerable<ChatHistory>> GetBySessionIdAsync(Guid sessionId);
        Task<IEnumerable<ChatHistory>> GetByUserIdAsync(int userId);
        Task AddAsync(ChatHistory chatHistory);
        Task DeleteSessionAsync(Guid sessionId);
        Task SaveChangesAsync();
    }
}
