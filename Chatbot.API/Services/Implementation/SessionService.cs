using Chatbot.API.Core.DTOs;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Chatbot.API.Services.Interface;

namespace Chatbot.API.Services.Implementation
{
    public class SessionService : ISessionService
    {
        private readonly IChatSessionRepository _sessionRepository;

        public SessionService(IChatSessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public async Task<ChatSessionResponseDto> CreateSessionAsync(int userId, CreateSessionDto dto)
        {
            var session = new ChatSession
            {
                SessionId = Guid.NewGuid(),
                Title = string.IsNullOrEmpty(dto.Title) ? "New Chat" : dto.Title,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _sessionRepository.AddAsync(session);
            await _sessionRepository.SaveChangesAsync();

            return new ChatSessionResponseDto
            {
                SessionId = session.SessionId,
                Title = session.Title,
                CreatedAt = session.CreatedAt,
                LastMessageAt = session.LastMessageAt
            };
        }

        public async Task<IEnumerable<ChatSessionResponseDto>> GetSessionsAsync(int userId)
        {
            var sessions = await _sessionRepository.GetByUserIdAsync(userId);

            return sessions.Select(s => new ChatSessionResponseDto
            {
                SessionId = s.SessionId,
                Title = s.Title,
                CreatedAt = s.CreatedAt,
                LastMessageAt = s.LastMessageAt
            });
        }

        public async Task<string> DeleteSessionAsync(int userId, Guid sessionId)
        {
            var session = await _sessionRepository.GetBySessionIdAsync(sessionId);

            if (session == null)
                return "Not found";

            if (session.UserId != userId)
                return "Forbidden";

            await _sessionRepository.DeleteAsync(sessionId);
            await _sessionRepository.SaveChangesAsync();

            return "Session deleted";
        }
    }
}