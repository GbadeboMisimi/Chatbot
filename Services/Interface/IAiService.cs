namespace Chatbot.API.Services.Interface
{
    public interface IAiService
    {
        Task<string> GetResponseAsync(string question, string context);
        Task<string> GetResponseWithHistoryAsync(
            string question,
            string context,
            List<(string Role, string Message)> history);
        Task<bool> IsAvailableAsync();
        Task<string> SummarizeContextAsync(string context);
        Task<string> CorrectSpellingAsync(string text);
        string GetFallbackResponse();
    }
}
