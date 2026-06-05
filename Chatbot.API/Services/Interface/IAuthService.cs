using Chatbot.API.Core.DTOs;

namespace Chatbot.API.Services.Interface
{
    public interface IAuthService
    {
        Task<ServiceResponseDto> RegisterAsync(RegisterDto dto);
        Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto);
    }
}