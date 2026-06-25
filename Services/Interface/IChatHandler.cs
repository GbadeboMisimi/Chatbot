using Chatbot.API.Core.DTOs;

namespace Chatbot.API.Services.Interface
{
    public interface IChatHandler
    {
        Task<ChatResponseDto> HandleAsync(int userId, ChatRequestDto request);
    }
}
