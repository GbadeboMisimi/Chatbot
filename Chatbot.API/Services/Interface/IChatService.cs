using Chatbot.API.Core.DTOs;

namespace Chatbot.API.Services.Interface
{
    public interface IChatService
    {
        Task<ChatResponseDto> SendMessageAsync(int userId, ChatRequestDto request);
        Task<IEnumerable<ChatHistoryResponseDto>> GetSessionHistoryAsync(int userId, Guid sessionId);
        Task<IEnumerable<ChatHistoryResponseDto>> GetAllHistoryAsync(int userId);
        Task<string> DeleteSessionHistoryAsync(int userId, Guid sessionId);
    }
}