using Chatbot.API.Services.Interface;
using Newtonsoft.Json;
using System.Text;

namespace Chatbot.API.Services.Implementation
{
    public class AiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiService> _logger;
        private readonly string _apiKey;
        private readonly string _apiUrl;

        public AiService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<AiService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _logger = logger;
            _apiKey = _configuration["GeminiSettings:ApiKey"]!;
            _apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}";
        }

        public async Task<string> GetResponseAsync(string question, string context)
        {
            var prompt = BuildPrompt(question, context);
            return await CallGeminiAsync(prompt);
        }

        public async Task<string> GetResponseWithHistoryAsync(
            string question,
            string context,
            List<(string Role, string Message)> history)
        {
            var prompt = BuildPromptWithHistory(question, context, history);
            return await CallGeminiAsync(prompt);
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var testPrompt = "Say hello";
                var result = await CallGeminiAsync(testPrompt);
                return !string.IsNullOrEmpty(result);
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> SummarizeContextAsync(string context)
        {
            if (context.Length < 1000)
                return context;

            var prompt = $@"
                Summarize the following content in a clear and concise way, 
                keeping all important facts and details:

                {context}

                Summary:";

            return await CallGeminiAsync(prompt);
        }

        public string GetFallbackResponse()
        {
            return "I'm sorry, I don't have enough information to answer that question. " +
                   "Please contact UBA directly at cfc@ubagroup.com or call 07002255822.";
        }

        private async Task<string> CallGeminiAsync(string prompt)
        {
            try
            {
                var body = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.2,
                        maxOutputTokens = 1024
                    }
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(body),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(_apiUrl, content);
                var result = await response.Content.ReadAsStringAsync();

                dynamic json = JsonConvert.DeserializeObject(result)!;
                return json.candidates[0].content.parts[0].text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini API call failed");
                return GetFallbackResponse();
            }
        }




        private string BuildPrompt(string question, string context)
        {
            if (string.IsNullOrEmpty(context))
                return GetFallbackResponse();

            return $@"
You are a helpful and honest assistant for UBA (United Bank for Africa).

STRICT RULES:
1. ONLY use information from the context below to answer
2. If the answer is not in the context, say exactly: ""I don't have that information. Please contact UBA directly at cfc@ubagroup.com or call 07002255822.""
3. NEVER make up facts, names, numbers or dates
4. NEVER use your own knowledge about UBA — only use the context
5. Keep answers clear and concise

Context:
{context}

Question:
{question}

Answer:";
        }


        //private string BuildPrompt(string question, string context)
        //{
        //    if (string.IsNullOrEmpty(context))
        //        return GetFallbackResponse();

        //    return $@"
        //        You are a helpful assistant for UBA (United Bank for Africa).
        //        Your job is to answer questions about UBA clearly and accurately.
        //        Use ONLY the context provided below to answer the question.
        //        If the context does not contain enough information, say you don't have that information and suggest contacting UBA directly.
        //        Do NOT make up information.

        //        Context:
        //        {context}

        //        Question:
        //        {question}

        //        Answer:";
        //}

        private string BuildPromptWithHistory(
            string question,
            string context,
            List<(string Role, string Message)> history)
        {
            var historyText = new StringBuilder();

            foreach (var (role, message) in history)
            {
                historyText.AppendLine($"{role}: {message}");
            }

            return $@"
                   
                You are a helpful and honest assistant for UBA (United Bank for Africa).
                STRICT RULES:
                1. ONLY use information from the context below to answer
                2. If the answer is not in the context, say exactly: ""I don't have that information. Please contact UBA directly at cfc@ubagroup.com or call 07002255822.""
                3. NEVER make up facts, names, numbers or dates
                4. NEVER use your own knowledge about UBA — only use the context
                5. Keep answers clear and concise
                Context:
                {context}

                Conversation History:
                {historyText}

                Question:
                {question}

                Answer:";
        }
    }
}


 //You are a helpful assistant for UBA (United Bank for Africa).
                //Your job is to answer questions about UBA clearly and accurately.
                //Use ONLY the context provided below to answer the question.
                //If the context does not contain enough information, say you don't have that information and suggest contacting UBA directly.
                //Do NOT make up information.