using Chatbot.API.Core.Models;

namespace Chatbot.API.Repositories.Interface
{
    public interface IChatSessionRepository
    {
        Task<ChatSession?> GetBySessionIdAsync(Guid sessionId);
        Task<IEnumerable<ChatSession>> GetByUserIdAsync(int userId);
        Task AddAsync(ChatSession session);
        Task UpdateAsync(ChatSession session);
        Task DeleteAsync(Guid sessionId);
        Task SaveChangesAsync();
    }
}