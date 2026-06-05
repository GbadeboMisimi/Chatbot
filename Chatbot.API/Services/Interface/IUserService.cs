using Chatbot.API.Core.DTOs;
using Chatbot.API.Core.Models;

namespace Chatbot.API.Services.Interface
{
    public interface IUserService
    {
        Task<IEnumerable<UserResponseDto>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(int id);
        Task<string> UpdateUserAsync(int id, UpdateUserDto dto);
        Task<string> DeleteUserAsync(int id);
    }
}
