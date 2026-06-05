namespace Chatbot.API.Core.DTOs
{
    public class ChatRequestDto
    {
        public string Message { get; set; }
        public Guid SessionId { get; set; }
    }
}
