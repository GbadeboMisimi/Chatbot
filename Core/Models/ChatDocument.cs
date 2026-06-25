namespace Chatbot.API.Core.Models
{
    public class ChatDocument
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public string Topic { get; set; }
        public string Category { get; set; }
        public string Content { get; set; }
        public string? Embedding { get; set; }
        public DateTime LastScraped { get; set; }
    }
}
