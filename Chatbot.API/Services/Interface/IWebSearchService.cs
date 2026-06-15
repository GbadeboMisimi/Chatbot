namespace Chatbot.API.Services.Interface
{
    public interface IWebSearchService
    {
        Task<(string Content, string Url)?> SearchUbaWebsiteAsync(string query);
    }
}
