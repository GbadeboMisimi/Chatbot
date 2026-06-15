namespace Chatbot.API.Core.DTOs
{
    public class ChatRequestDto
    {
        public string Message { get; set; } = string.Empty;
        public Guid SessionId { get; set; }
    }
}
