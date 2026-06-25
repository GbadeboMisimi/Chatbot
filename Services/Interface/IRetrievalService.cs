using Chatbot.API.Core.DTOs;
using Chatbot.API.Core.Models;

namespace Chatbot.API.Services.Interface
{
    public interface IRetrievalService
    {
        Task<IEnumerable<ChatDocument>> GetRelevantDocumentsAsync(string query, int topK = 10);
        Task<string> BuildContextAsync(string query);
        Task<(string Context, List<CitationDto> Citations)> BuildContextWithCitationsAsync(string query);
        Task<IEnumerable<ChatDocument>> GetByCategoryAsync(string category, int topK = 5);
        Task<bool> HasRelevantDocumentsAsync(string query);
    }
}