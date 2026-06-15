using Chatbot.API.Core.DTOs;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Chatbot.API.Services.Interface;

namespace Chatbot.API.Services.Implementation
{
    public class ChatService : IChatService
    {
        private readonly IAiService _aiService;
        private readonly IRetrievalService _retrievalService;
        private readonly IChatHistoryRepository _chatHistoryRepository;
        private readonly IChatSessionRepository _sessionRepository;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IAiService aiService,
            IRetrievalService retrievalService,
            IChatHistoryRepository chatHistoryRepository,
            IChatSessionRepository sessionRepository,
            ILogger<ChatService> logger)
        {
            _aiService = aiService;
            _retrievalService = retrievalService;
            _chatHistoryRepository = chatHistoryRepository;
            _sessionRepository = sessionRepository;
            _logger = logger;
        }

        public async Task<ChatResponseDto> SendMessageAsync(int userId, ChatRequestDto request)
        {
            // Handle session
            var session = await _sessionRepository.GetBySessionIdAsync(request.SessionId);
            if (session == null)
            {
                session = new ChatSession
                {
                    SessionId = request.SessionId == Guid.Empty ? Guid.NewGuid() : request.SessionId,
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
                SessionId = session.SessionId,
                Role = "user",
                Message = request.Message,
                SentAt = DateTime.UtcNow
            });
            await _chatHistoryRepository.SaveChangesAsync();

            string reply;
            List<CitationDto> citations = new();

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
                    var (context, sourceCitations) = await _retrievalService.BuildContextWithCitationsAsync(request.Message);
                    citations = sourceCitations;

                    var rawHistory = await _chatHistoryRepository.GetBySessionIdAsync(session.SessionId);
                    var history = rawHistory.Select(h => (h.Role, h.Message)).ToList();

                    reply = await _aiService.GetResponseWithHistoryAsync(request.Message, context, history);
                }
            }

            // Save AI response
            await _chatHistoryRepository.AddAsync(new ChatHistory
            {
                UserId = userId,
                SessionId = session.SessionId,
                Role = "Assistant",
                Message = reply,
                SentAt = DateTime.UtcNow
            });
            await _chatHistoryRepository.SaveChangesAsync();

            return new ChatResponseDto
            {
                Reply = reply,
                SessionId = session.SessionId,
                Sources = citations
            };
        }

        public async Task<IEnumerable<ChatHistoryResponseDto>> GetSessionHistoryAsync(int userId, Guid sessionId)
        {
            var history = await _chatHistoryRepository.GetBySessionIdAsync(sessionId);

            return history
                .Where(h => h.UserId == userId)
                .Select(h => new ChatHistoryResponseDto
                {
                    Id = h.Id,
                    Role = h.Role,
                    Message = h.Message,
                    SentAt = h.SentAt,
                    SessionId = h.SessionId
                });
        }

        public async Task<IEnumerable<ChatHistoryResponseDto>> GetAllHistoryAsync(int userId)
        {
            var history = await _chatHistoryRepository.GetByUserIdAsync(userId);

            return history.Select(h => new ChatHistoryResponseDto
            {
                Id = h.Id,
                Role = h.Role,
                Message = h.Message,
                SentAt = h.SentAt,
                SessionId = h.SessionId
            });
        }

        public async Task<string> DeleteSessionHistoryAsync(int userId, Guid sessionId)
        {
            var history = await _chatHistoryRepository.GetBySessionIdAsync(sessionId);

            if (!history.Any(h => h.UserId == userId))
                return "Not found";

            await _chatHistoryRepository.DeleteSessionAsync(sessionId);
            await _chatHistoryRepository.SaveChangesAsync();

            return "Session deleted successfully";
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
    }
}