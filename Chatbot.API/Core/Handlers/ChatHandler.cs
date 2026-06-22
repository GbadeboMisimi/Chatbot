using Chatbot.API.Core.DTOs;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Chatbot.API.Services.Interface;

namespace Chatbot.API.Core.Handlers
{
    public class ChatHandler : IChatHandler
    {
        private readonly IAiService _aiService;
        private readonly IRetrievalService _retrievalService;
        private readonly IChatHistoryRepository _chatHistoryRepository;
        private readonly IChatSessionRepository _sessionRepository;
        private readonly IWebSearchService _webSearchService;
        private readonly ILogger<ChatHandler> _logger;

        public ChatHandler(
            IAiService aiService,
            IRetrievalService retrievalService,
            IChatHistoryRepository chatHistoryRepository,
            IChatSessionRepository sessionRepository,
            IWebSearchService webSearchService,
            ILogger<ChatHandler> logger)
        {
            _aiService = aiService;
            _retrievalService = retrievalService;
            _chatHistoryRepository = chatHistoryRepository;
            _sessionRepository = sessionRepository;
            _webSearchService = webSearchService;
            _logger = logger;
        }

        public async Task<ChatResponseDto> HandleAsync(int userId, ChatRequestDto request)
        {
            request.Message = request.Message.Trim();

            // Handle session
            var session = request.SessionId.HasValue
                ? await _sessionRepository.GetBySessionIdAsync(request.SessionId.Value)
                : null;

            if (session == null)
            {
                session = new ChatSession
                {
                    SessionId = request.SessionId.HasValue && request.SessionId.Value != Guid.Empty
                        ? request.SessionId.Value
                        : Guid.NewGuid(),
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
                if (session.UserId != userId)
                    throw new UnauthorizedAccessException(
                        "You do not have access to this chat session.");

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

            // Greeting
            if (IsGreeting(request.Message))
            {
                reply =
                    "Hello! Welcome to UBA. How can I help you today? " +
                    "You can ask me about UBA's history, leadership, careers, " +
                    "global presence, contact information, and more.";
            }
            // AI unavailable
            else if (!await _aiService.IsAvailableAsync())
            {
                reply = "AI service is currently unavailable. Please try again later.";
            }
            else
            {
                string correctedMessage;

                try
                {
                    correctedMessage =
                        await _aiService.CorrectSpellingAsync(request.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Spelling correction failed. Using original query.");

                    correctedMessage = request.Message;
                }

                var normalizedMessage = NormalizeQuery(correctedMessage);

                _logger.LogInformation(
                    "Original query: {Original} | Corrected query: {Corrected}",
                    request.Message,
                    correctedMessage);

                // Block product/service questions
                if (IsProductOrServiceQuestion(normalizedMessage))
                {
                    reply =
                        "For questions about UBA products and services, " +
                        "please visit ubagroup.com or speak to a UBA representative.";
                }
                else
                {
                    var (context, sourceCitations) =
                        await _retrievalService.BuildContextWithCitationsAsync(
                            normalizedMessage);

                    bool hasRelevantDocs =
                        !string.IsNullOrWhiteSpace(context);

                    // DATABASE MISSED -> WEBSITE FALLBACK
                    if (!hasRelevantDocs)
                    {
                        _logger.LogInformation(
                            "No database context found. Searching UBA website.");

                        var liveResult =
                            await _webSearchService.SearchUbaWebsiteAsync(
                                normalizedMessage);

                        if (liveResult != null)
                        {
                            _logger.LogInformation("Website fallback succeeded. URL: {Url}", liveResult.Value.Url);
                            

                            _logger.LogInformation(
                               "Content length sent to AI: {Length}",
                               liveResult.Value.Content?.Length ?? 0);

                            reply = await _aiService.GetResponseAsync(
                                correctedMessage,
                                liveResult.Value.Content);
                            citations.Add(new CitationDto
                            {
                                Topic = "UBA Website (Live)",
                                Url = liveResult.Value.Url,
                                Category = "live"
                            });
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Website fallback returned NULL for query: {Query}",
                                normalizedMessage);

                            reply = _aiService.GetFallbackResponse();
                        }
                    }
                    else
                    {
                        citations = sourceCitations;

                        var rawHistory =
                            await _chatHistoryRepository.GetBySessionIdAsync(
                                session.SessionId);

                        var history = rawHistory
                            .Select(h => (h.Role, h.Message))
                            .ToList();

                        reply = await _aiService.GetResponseWithHistoryAsync(
                            correctedMessage,
                            context,
                            history);

                        // AI says no answer -> WEBSITE FALLBACK
                        if (IsFallbackReply(reply))
                        {
                            _logger.LogInformation(
                                "Database answer insufficient. Searching UBA website.");

                            var liveResult =
                                await _webSearchService.SearchUbaWebsiteAsync(
                                    normalizedMessage);

                            if (liveResult != null)
                            {
                                reply = await _aiService.GetResponseAsync(
                                    correctedMessage,
                                    liveResult.Value.Content);

                                citations = new List<CitationDto>
                                {
                                    new CitationDto
                                    {
                                        Topic = "UBA Website (Live)",
                                        Url = liveResult.Value.Url,
                                        Category = "live"
                                    }
                                };
                            }
                        }
                    }
                }
            }

            // Save assistant message
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

        private static string NormalizeQuery(string message)
        {
            return string.Join(" ", message
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool IsProductOrServiceQuestion(string message)
        {
            var lower = message.ToLowerInvariant();
            var productTerms = new[]
            {
                "account", "accounts", "loan", "loans", "card", "cards", "transfer",
                "mobile banking", "internet banking", "ussd", "atm", "fee", "fees",
                "interest rate", "open an account", "bank app", "payment", "payments"
            };

            return productTerms.Any(lower.Contains);
        }


        private static readonly string[] FallbackIndicators =
        {
            "I don't have enough information",
            "I don't have that information",
            "not available in the provided context",
            "cannot determine from the context",
            "information is not provided"
        };

        private static bool IsFallbackReply(string reply)
        {
            return FallbackIndicators.Any(
                indicator => reply.Contains(
                    indicator,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}
