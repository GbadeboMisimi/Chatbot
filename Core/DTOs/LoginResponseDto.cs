namespace Chatbot.API.Core.DTOs
{
    public class LoginResponseDto
    {
        public string FullName { get; set; }
        public string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }
}
