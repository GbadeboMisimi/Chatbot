using Chatbot.API.Core.DTOs;
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
        private readonly IChatService _chatService;
        private readonly IRetrievalService _retrievalService;

        public ChatController(
            IChatService chatService,
            IRetrievalService retrievalService)
        {
            _chatService = chatService;
            _retrievalService = retrievalService;
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequestDto request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message is required");

            try
            {
                var result = await _chatService.SendMessageAsync(userId, request);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("history/{sessionId}")]
        public async Task<IActionResult> GetSessionHistory(Guid sessionId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _chatService.GetSessionHistoryAsync(userId, sessionId);
            return Ok(result);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetAllHistory()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _chatService.GetAllHistoryAsync(userId);
            return Ok(result);
        }

        [HttpDelete("history/{sessionId}")]
        public async Task<IActionResult> DeleteSession(Guid sessionId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _chatService.DeleteSessionHistoryAsync(userId, sessionId);

            if (result == "Not found") return NotFound(result);
            return Ok(result);
        }

        [HttpGet("test-context")]
        public async Task<IActionResult> TestContext([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query is required");

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
                ContextPreview = context.Length > 0
                    ? context.Substring(0, Math.Min(500, context.Length))
                    : string.Empty
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
