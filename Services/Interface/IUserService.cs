using Chatbot.API.Core.DTOs;
namespace Chatbot.API.Services.Interface
{
    public interface IUserService
    {
        Task<IEnumerable<UserResponseDto>> GetAllUsersAsync();
        Task<UserResponseDto?> GetUserByIdAsync(int id);
        Task<string> UpdateUserAsync(int id, UpdateUserDto dto);
        Task<string> DeleteUserAsync(int id);
    }
}
