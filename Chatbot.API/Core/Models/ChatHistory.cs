namespace Chatbot.API.Core.Models
{
    public class ChatHistory
    {
        public int Id { get; set; }
        public Guid SessionId { get; set; }
        public string Role { get; set; }
        public string Message { get; set; }
        public DateTime SentAt { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }
    }
}
