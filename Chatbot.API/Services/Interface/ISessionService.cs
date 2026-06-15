using Chatbot.API.Core.DTOs;

namespace Chatbot.API.Services.Interface
{
    public interface ISessionService
    {
        Task<ChatSessionResponseDto> CreateSessionAsync(int userId, CreateSessionDto dto);
        Task<IEnumerable<ChatSessionResponseDto>> GetSessionsAsync(int userId);
        Task<string> DeleteSessionAsync(int userId, Guid sessionId);
    }
}