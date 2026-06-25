using Chatbot.API.Core.DTOs;

namespace Chatbot.API.Services.Interface
{
    public interface IScraperService
    {
        Task<string> ScrapeAllAsync();
        Task<string> ScrapePageAsync(string url);
        Task<ScraperStatusDto> GetScraperStatusAsync();
    }
}