namespace Chatbot.API.Core.DTOs
{
    public class ScraperStatusDto
    {
        public int TotalDocuments { get; set; }
        public DateTime? LastScraped { get; set; }
        public List<string> ScrapedUrls { get; set; }
        public int TotalUrlsConfigured { get; set; }
        public int TotalUrlsScraped { get; set; }
    }
}