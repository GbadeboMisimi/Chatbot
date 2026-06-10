using Chatbot.API.Core.DTOs;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Chatbot.API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Chatbot.API.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IAiService _aiService;
        private readonly IRetrievalService _retrievalService;
        private readonly IChatHistoryRepository _chatHistoryRepository;
        private readonly IChatSessionRepository _sessionRepository;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IAiService aiService,
            IRetrievalService retrievalService,
            IChatHistoryRepository chatHistoryRepository,
            IChatSessionRepository sessionRepository,
            ILogger<ChatController> logger)
        {
            _aiService = aiService;
            _retrievalService = retrievalService;
            _chatHistoryRepository = chatHistoryRepository;
            _sessionRepository = sessionRepository;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequestDto request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var session = await _sessionRepository.GetBySessionIdAsync(request.SessionId);
            if (session == null)
            {
                session = new ChatSession
                {
                    SessionId = request.SessionId,
                    Title = request.Message.Length > 50
                        ? request.Message.Substring(0, 50) + "..."
                        : request.Message,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow
                };
                await _sessionRepository.AddAsync(session);
            }
            else
            {
                session.LastMessageAt = DateTime.UtcNow;
                await _sessionRepository.UpdateAsync(session);
            }
            await _sessionRepository.SaveChangesAsync();

            // Save user message
            await _chatHistoryRepository.AddAsync(new ChatHistory
            {
                UserId = userId,
                SessionId = request.SessionId,
                Role = "user",
                Message = request.Message,
                SentAt = DateTime.UtcNow
            });
            await _chatHistoryRepository.SaveChangesAsync();

            string reply;

            // Handle greetings
            if (IsGreeting(request.Message))
            {
                reply = "Hello! Welcome to UBA. How can I help you today? You can ask me about UBA's history, leadership, careers, global presence, contact information, and more.";
            }
            else if (!await _aiService.IsAvailableAsync())
            {
                reply = "AI service is currently unavailable. Please try again later.";
            }
            else
            {
                var hasRelevantDocs = await _retrievalService.HasRelevantDocumentsAsync(request.Message);

                if (!hasRelevantDocs)
                {
                    reply = _aiService.GetFallbackResponse();
                }
                else
                {
                    var context = await _retrievalService.BuildContextAsync(request.Message);
                    var rawHistory = await _chatHistoryRepository.GetBySessionIdAsync(request.SessionId);
                    var history = rawHistory.Select(h => (h.Role, h.Message)).ToList();
                    reply = await _aiService.GetResponseWithHistoryAsync(request.Message, context, history);
                }
            }

            // Save AI response
            await _chatHistoryRepository.AddAsync(new ChatHistory
            {
                UserId = userId,
                SessionId = request.SessionId,
                Role = "assistant",
                Message = reply,
                SentAt = DateTime.UtcNow
            });
            await _chatHistoryRepository.SaveChangesAsync();

            return Ok(new ChatResponseDto
            {
                Reply = reply,
                SessionId = request.SessionId
            });
        }

        private bool IsGreeting(string message)
        {
            var greetings = new[]
            {
        "hi", "hello", "hey", "good morning", "good afternoon",
        "good evening", "howdy", "greetings", "how are you",
        "what's up", "whats up", "sup"
    };

            var lower = message.ToLower().Trim();
            return greetings.Any(g => lower == g || lower.StartsWith(g + " ") || lower.EndsWith(" " + g));
        }

        [HttpGet("history/{sessionId}")]
        public async Task<IActionResult> GetSessionHistory(Guid sessionId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var history = await _chatHistoryRepository.GetBySessionIdAsync(sessionId);

            // Make sure user can only see their own history
            var userHistory = history.Where(h => h.UserId == userId).ToList();

            return Ok(userHistory);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetAllHistory()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var history = await _chatHistoryRepository.GetByUserIdAsync(userId);

            return Ok(history);
        }

        [HttpDelete("history/{sessionId}")]
        public async Task<IActionResult> DeleteSession(Guid sessionId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Verify session belongs to user
            var history = await _chatHistoryRepository.GetBySessionIdAsync(sessionId);
            if (!history.Any(h => h.UserId == userId))
                return NotFound("Session not found");

            await _chatHistoryRepository.DeleteSessionAsync(sessionId);
            await _chatHistoryRepository.SaveChangesAsync();

            return Ok("Session deleted successfully");
        }


        [HttpGet("test-context")]
        public async Task<IActionResult> TestContext([FromQuery] string query)
        {
            var docs = await _retrievalService.GetRelevantDocumentsAsync(query);
            var context = await _retrievalService.BuildContextAsync(query);

            return Ok(new
            {
                DocumentCount = docs.Count(),
                Documents = docs.Select(d => new
                {
                    d.Topic,
                    d.Category,
                    ContentPreview = d.Content.Substring(0, Math.Min(150, d.Content.Length)),
                    HasEmbedding = !string.IsNullOrEmpty(d.Embedding)
                }),
                ContextPreview = context.Substring(0, Math.Min(500, context.Length))
            });
        }


    }
}








//[HttpPost]
//public async Task<IActionResult> Chat([FromBody] ChatRequestDto request)
//{
//    // Get logged in user ID from JWT token
//    var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

//    // Check if AI is available
//    if (!await _aiService.IsAvailableAsync())
//        return StatusCode(503, "AI service is currently unavailable. Please try again later.");

//    // Save user message to history
//    await _chatHistoryRepository.AddAsync(new ChatHistory
//    {
//        UserId = userId,
//        SessionId = request.SessionId,
//        Role = "user",
//        Message = request.Message,
//        SentAt = DateTime.UtcNow
//    });
//    await _chatHistoryRepository.SaveChangesAsync();

//    // Check if relevant documents exist
//    var hasRelevantDocs = await _retrievalService.HasRelevantDocumentsAsync(request.Message);

//    string reply;

//    if (!hasRelevantDocs)
//    {
//        reply = _aiService.GetFallbackResponse();
//    }
//    else
//    {
//        // Build context from relevant documents
//        var context = await _retrievalService.BuildContextAsync(request.Message);

//        // Get conversation history for this session
//        var rawHistory = await _chatHistoryRepository.GetBySessionIdAsync(request.SessionId);
//        var history = rawHistory
//            .Select(h => (h.Role, h.Message))
//            .ToList();

//        // Get AI response
//        reply = await _aiService.GetResponseWithHistoryAsync(
//            request.Message,
//            context,
//            history);
//    }

//    // Save AI response to history
//    await _chatHistoryRepository.AddAsync(new ChatHistory
//    {
//        UserId = userId,
//        SessionId = request.SessionId,
//        Role = "assistant",
//        Message = reply,
//        SentAt = DateTime.UtcNow
//    });
//    await _chatHistoryRepository.SaveChangesAsync();

//    return Ok(new ChatResponseDto
//    {
//        Reply = reply,
//        SessionId = request.SessionId
//    });
//}