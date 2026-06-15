using Chatbot.API.Core.DTOs;
using Chatbot.API.Services.Interface;
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
        private readonly ISessionService _sessionService;

        public SessionController(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateSession([FromBody] CreateSessionDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _sessionService.CreateSessionAsync(userId, dto);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetSessions()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _sessionService.GetSessionsAsync(userId);
            return Ok(result);
        }

        [HttpDelete("{sessionId}")]
        public async Task<IActionResult> DeleteSession(Guid sessionId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _sessionService.DeleteSessionAsync(userId, sessionId);

            if (result == "Not found") return NotFound(result);
            if (result == "Forbidden") return Forbid();

            return Ok(result);
        }
    }
}