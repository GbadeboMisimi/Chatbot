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
        private readonly IWebSearchService _webSearchService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IAiService aiService,
            IRetrievalService retrievalService,
            IChatHistoryRepository chatHistoryRepository,
            IChatSessionRepository sessionRepository,
            IWebSearchService webSearchService,
            ILogger<ChatService> logger)
        {
            _aiService = aiService;
            _retrievalService = retrievalService;
            _chatHistoryRepository = chatHistoryRepository;
            _sessionRepository = sessionRepository;
            _webSearchService = webSearchService;
            _logger = logger;
        }

        public async Task<ChatResponseDto> SendMessageAsync(int userId, ChatRequestDto request)
        {
            request.Message = request.Message.Trim();

            // Handle session
            var session = await _sessionRepository.GetBySessionIdAsync(request.SessionId);

            if (session == null)
            {
                session = new ChatSession
                {
                    SessionId = request.SessionId == Guid.Empty
                        ? Guid.NewGuid()
                        : request.SessionId,
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











//public async Task<ChatResponseDto> SendMessageAsync(int userId, ChatRequestDto request)
//{
//    request.Message = request.Message.Trim();

//    // Handle session
//    var session = await _sessionRepository.GetBySessionIdAsync(request.SessionId);
//    if (session == null)
//    {
//        session = new ChatSession
//        {
//            SessionId = request.SessionId == Guid.Empty ? Guid.NewGuid() : request.SessionId,
//            Title = request.Message.Length > 50
//                ? request.Message.Substring(0, 50) + "..."
//                : request.Message,
//            UserId = userId,
//            CreatedAt = DateTime.UtcNow,
//            LastMessageAt = DateTime.UtcNow
//        };
//        await _sessionRepository.AddAsync(session);
//    }
//    else
//    {
//        if (session.UserId != userId)
//            throw new UnauthorizedAccessException("You do not have access to this chat session.");

//        session.LastMessageAt = DateTime.UtcNow;
//        await _sessionRepository.UpdateAsync(session);
//    }
//    await _sessionRepository.SaveChangesAsync();

//    // Save user message
//    await _chatHistoryRepository.AddAsync(new ChatHistory
//    {
//        UserId = userId,
//        SessionId = session.SessionId,
//        Role = "user",
//        Message = request.Message,
//        SentAt = DateTime.UtcNow
//    });
//    await _chatHistoryRepository.SaveChangesAsync();

//    string reply;
//    List<CitationDto> citations = new();

//    // Handle greetings
//    if (IsGreeting(request.Message))
//    {
//        reply = "Hello! Welcome to UBA. How can I help you today? You can ask me about UBA's history, leadership, careers, global presence, contact information, and more.";
//    }
//    else if (!await _aiService.IsAvailableAsync())
//    {
//        reply = "AI service is currently unavailable. Please try again later.";
//    }
//    else
//    {




//        string correctedMessage;

//        try
//        {
//            correctedMessage =
//                await _aiService.CorrectSpellingAsync(request.Message);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogWarning(
//                ex,
//                "Spelling correction failed. Using original query.");

//            correctedMessage = request.Message;
//        }
//        var normalizedMessage = NormalizeQuery(correctedMessage);



//        //var correctedMessage = await _aiService.CorrectSpellingAsync(request.Message);
//        //var normalizedMessage = NormalizeQuery(correctedMessage);

//        _logger.LogInformation(
//            "Original chat query: {Original} | Corrected query: {Corrected}",
//            request.Message,
//            correctedMessage);

//        if (IsProductOrServiceQuestion(normalizedMessage))
//        {
//            reply = "For questions about UBA products and services, please visit ubagroup.com or speak to a UBA representative.";
//        }
//        else
//        {


//            var (context, sourceCitations) =
//            await _retrievalService.BuildContextWithCitationsAsync(normalizedMessage);

//            var hasRelevantDocs = !string.IsNullOrWhiteSpace(context);

//            if (!hasRelevantDocs)
//            {
//                _logger.LogInformation(
//                    "No database context found. Trying UBA-only live fallback for: {Query}",
//                    normalizedMessage);

//                var liveResult =
//                    await _webSearchService.SearchUbaWebsiteAsync(normalizedMessage);

//                if (liveResult != null)
//                {
//                    _logger.LogInformation(
//                        "Live UBA website match found: {Url}",
//                        liveResult.Value.Url);

//                    reply = await _aiService.GetResponseAsync(
//                        correctedMessage,
//                        liveResult.Value.Content);

//                    citations.Add(new CitationDto
//                    {
//                        Topic = "UBA Website (Live)",
//                        Url = liveResult.Value.Url,
//                        Category = "live"
//                    });
//                }
//                else
//                {
//                    reply = _aiService.GetFallbackResponse();
//                }
//            }
//            else
//            {
//                citations = sourceCitations;

//                var rawHistory =
//                    await _chatHistoryRepository.GetBySessionIdAsync(session.SessionId);

//                var history = rawHistory
//                    .Select(h => (h.Role, h.Message))
//                    .ToList();

//                reply = await _aiService.GetResponseWithHistoryAsync(
//                    correctedMessage,
//                    context,
//                    history);

//                if (IsFallbackReply(reply))
//                {
//                    _logger.LogInformation(
//                        "Database context did not answer the query. Trying UBA-only live fallback for: {Query}",
//                        normalizedMessage);

//                    var liveResult =
//                        await _webSearchService.SearchUbaWebsiteAsync(normalizedMessage);

//                    if (liveResult != null)
//                    {
//                        _logger.LogInformation(
//                            "Live UBA website match found: {Url}",
//                            liveResult.Value.Url);

//                        reply = await _aiService.GetResponseAsync(
//                            correctedMessage,
//                            liveResult.Value.Content);

//                        citations = new List<CitationDto>
//    {
//        new CitationDto
//        {
//            Topic = "UBA Website (Live)",
//            Url = liveResult.Value.Url,
//            Category = "live"
//        }
//    };
//                    }
//                }
//            }






//            //var hasRelevantDocs = await _retrievalService.HasRelevantDocumentsAsync(normalizedMessage);

//            //if (!hasRelevantDocs)
//            //{
//            //    _logger.LogInformation(
//            //        "No database context found. Trying UBA-only live fallback for: {Query}",
//            //        normalizedMessage);

//            //    var liveResult = await _webSearchService.SearchUbaWebsiteAsync(normalizedMessage);
//            //    if (liveResult != null)
//            //    {
//            //        reply = await _aiService.GetResponseAsync(correctedMessage, liveResult.Value.Content);
//            //        citations.Add(new CitationDto
//            //        {
//            //            Topic = "UBA Website (Live)",
//            //            Url = liveResult.Value.Url,
//            //            Category = "live"
//            //        });
//            //    }
//            //    else
//            //    {
//            //        reply = _aiService.GetFallbackResponse();
//            //    }
//            //}
//            //else
//            //{
//            //    var (context, sourceCitations) = await _retrievalService.BuildContextWithCitationsAsync(normalizedMessage);
//            //    citations = sourceCitations;

//            //    var rawHistory = await _chatHistoryRepository.GetBySessionIdAsync(session.SessionId);
//            //    var history = rawHistory.Select(h => (h.Role, h.Message)).ToList();

//            //    reply = await _aiService.GetResponseWithHistoryAsync(correctedMessage, context, history);

//            //    if (IsFallbackReply(reply))
//            //    {
//            //        _logger.LogInformation(
//            //            "Database context did not answer the query. Trying UBA-only live fallback for: {Query}",
//            //            normalizedMessage);

//            //        var liveResult = await _webSearchService.SearchUbaWebsiteAsync(normalizedMessage);
//            //        if (liveResult != null)
//            //        {
//            //            reply = await _aiService.GetResponseAsync(correctedMessage, liveResult.Value.Content);
//            //            citations = new List<CitationDto>
//            //            {
//            //                new()
//            //                {
//            //                    Topic = "UBA Website (Live)",
//            //                    Url = liveResult.Value.Url,
//            //                    Category = "live"
//            //                }
//            //            };
//            //        }
//            //    }
//            //}
//            //}
//            //}

//            // Save AI response
//            await _chatHistoryRepository.AddAsync(new ChatHistory
//            {
//                UserId = userId,
//                SessionId = session.SessionId,
//                Role = "Assistant",
//                Message = reply,
//                SentAt = DateTime.UtcNow
//            });
//            await _chatHistoryRepository.SaveChangesAsync();

//            // Save AI response
//            await _chatHistoryRepository.AddAsync(new ChatHistory
//            {
//                UserId = userId,
//                SessionId = session.SessionId,
//                Role = "Assistant",
//                Message = reply,
//                SentAt = DateTime.UtcNow
//            });

//            await _chatHistoryRepository.SaveChangesAsync();

//            return new ChatResponseDto
//            {
//                Reply = reply,
//                SessionId = session.SessionId,
//                Sources = citations
//            };
//        }
//    }
//}




//private static bool IsFallbackReply(string reply)
//{
//    return reply.Contains("I don't have enough information", StringComparison.OrdinalIgnoreCase) ||
//           reply.Contains("I don't have that information", StringComparison.OrdinalIgnoreCase);
//}