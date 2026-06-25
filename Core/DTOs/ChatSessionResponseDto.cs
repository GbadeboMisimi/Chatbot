namespace Chatbot.API.Core.DTOs
{
    public class ChatSessionResponseDto
    {
        public Guid SessionId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
    }
}