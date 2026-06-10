namespace Chatbot.API.Core.Models
{
    public class ChatSession
    {
        public int Id { get; set; }
        public Guid SessionId { get; set; }
        public string Title { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }

        public User User { get; set; }
        public ICollection<ChatHistory> ChatHistories { get; set; }
    }
}