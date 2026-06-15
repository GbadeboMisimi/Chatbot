namespace Chatbot.API.Core.DTOs
{
    public class ChatHistoryResponseDto
    {
        public int Id { get; set; }
        public string Role { get; set; }
        public string Message { get; set; }
        public DateTime SentAt { get; set; }
        public Guid SessionId { get; set; }
    }
}