using Chatbot.API.Core.DTOs;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Chatbot.API.Controllers
{
    [ApiController]
    [Route("api/sessions")]
    [Authorize]
    public class SessionController : ControllerBase
    {
        private readonly IChatSessionRepository _sessionRepository;

        public SessionController(IChatSessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        [HttpPost]
        public async Task<IActionResult> CreateSession([FromBody] CreateSessionDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var session = new ChatSession
            {
                SessionId = Guid.NewGuid(),
                Title = string.IsNullOrEmpty(dto.Title) ? "New Chat" : dto.Title,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _sessionRepository.AddAsync(session);
            await _sessionRepository.SaveChangesAsync();

            return Ok(new ChatSessionResponseDto
            {
                SessionId = session.SessionId,
                Title = session.Title,
                CreatedAt = session.CreatedAt,
                LastMessageAt = session.LastMessageAt
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetSessions()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var sessions = await _sessionRepository.GetByUserIdAsync(userId);

            var result = sessions.Select(s => new ChatSessionResponseDto
            {
                SessionId = s.SessionId,
                Title = s.Title,
                CreatedAt = s.CreatedAt,
                LastMessageAt = s.LastMessageAt
            });

            return Ok(result);
        }

        [HttpDelete("{sessionId}")]
        public async Task<IActionResult> DeleteSession(Guid sessionId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var session = await _sessionRepository.GetBySessionIdAsync(sessionId);

            if (session == null)
                return NotFound("Session not found");

            if (session.UserId != userId)
                return Forbid();

            await _sessionRepository.DeleteAsync(sessionId);
            await _sessionRepository.SaveChangesAsync();

            return Ok("Session deleted");
        }
    }
}