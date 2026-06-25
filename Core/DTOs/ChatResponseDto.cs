namespace Chatbot.API.Core.DTOs
{
    public class ChatResponseDto
    {
        public string Reply { get; set; }
        public Guid SessionId { get; set; }
        public List<CitationDto> Sources { get; set; } = new();

    }
}
