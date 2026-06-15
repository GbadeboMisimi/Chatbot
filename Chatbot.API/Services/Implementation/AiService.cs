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
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _apiKey = _configuration["GeminiSettings:ApiKey"]!;
            //_apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}";

            _apiUrl = "http://localhost:11434/api/generate";
        }

        public async Task<string> GetResponseAsync(string question, string context)
        {
            var prompt = BuildPrompt(question, context);
            return await CallOllamaAsync(prompt);
        }

        public async Task<string> GetResponseWithHistoryAsync(
            string question,
            string context,
            List<(string Role, string Message)> history)
        {
            var prompt = BuildPromptWithHistory(question, context, history);
            return await CallOllamaAsync(prompt);
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var testPrompt = "Say hello";
                var result = await CallOllamaAsync(testPrompt);
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

            return await CallOllamaAsync(prompt);
        }

        public async Task<string> CorrectSpellingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            try
            {
                var prompt = $@"
Fix spelling and obvious typing mistakes in this user question.
Return ONLY the corrected question. Do not answer it. Do not add explanation.
Preserve the user's meaning.

Question:
{text}

Corrected question:";

                var result = await CallOllamaAsync(prompt);
                var corrected = string.IsNullOrWhiteSpace(result) ? text : result.Trim().Trim('"');
                return ApplyUbaCorrections(corrected);
            }
            catch
            {
                return ApplyUbaCorrections(text);
            }
        }

        public string GetFallbackResponse()
        {
            return "I'm sorry, I don't have enough information to answer that question. " +
                   "Please contact UBA directly at cfc@ubagroup.com or call 07002255822.";
        }









        //private async Task<string> CallGeminiAsync(string prompt).... chatgpt
        //{
        //    int maxRetries = 3;
        //    int delayMs = 5000;

        //    for (int attempt = 1; attempt <= maxRetries; attempt++)
        //    {
        //        try
        //        {
        //            var body = new
        //            {
        //                contents = new[]
        //                {
        //            new
        //            {
        //                parts = new[]
        //                {
        //                    new { text = prompt }
        //                }
        //            }
        //        },
        //                generationConfig = new
        //                {
        //                    temperature = 0.2,
        //                    maxOutputTokens = 1024
        //                }
        //            };

        //            var content = new StringContent(
        //                JsonConvert.SerializeObject(body),
        //                Encoding.UTF8,
        //                "application/json");

        //            var response = await _httpClient.PostAsync(_apiUrl, content);
        //            var result = await response.Content.ReadAsStringAsync();

        //            // Log every response from Gemini
        //            _logger.LogInformation(
        //                "Gemini Status: {StatusCode}",
        //                response.StatusCode);

        //            _logger.LogInformation(
        //                "Gemini Response: {Response}",
        //                result);

        //            // Handle rate limiting
        //            if ((int)response.StatusCode == 429)
        //            {
        //                _logger.LogWarning(
        //                    "Rate limited. Attempt {Attempt}/{Max}. Waiting {Delay}ms",
        //                    attempt,
        //                    maxRetries,
        //                    delayMs);

        //                await Task.Delay(delayMs);
        //                delayMs *= 2;
        //                continue;
        //            }

        //            // Handle any other error responses
        //            if (!response.IsSuccessStatusCode)
        //            {
        //                _logger.LogError(
        //                    "Gemini Error. Status: {StatusCode}. Response: {Response}",
        //                    response.StatusCode,
        //                    result);

        //                return GetFallbackResponse();
        //            }

        //            dynamic json = JsonConvert.DeserializeObject(result)!;

        //            if (json?.candidates == null ||
        //                json.candidates.Count == 0)
        //            {
        //                _logger.LogWarning(
        //                    "Gemini returned no candidates. Response: {Response}",
        //                    result);

        //                return GetFallbackResponse();
        //            }

        //            return json.candidates[0].content.parts[0].text.ToString();
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(
        //                ex,
        //                "Gemini API call failed on attempt {Attempt}",
        //                attempt);

        //            if (attempt == maxRetries)
        //            {
        //                return GetFallbackResponse();
        //            }

        //            await Task.Delay(delayMs);
        //        }
        //    }

        //    return GetFallbackResponse();
        //}















//        private async Task<string> CallGeminiAsync(string prompt)//claude
//        {
//            int maxRetries = 3;
//        int delayMs = 5000;

//            for (int attempt = 1; attempt <= maxRetries; attempt++)
//            {
//                try
//                {
//                    var body = new
//                    {
//                        contents = new[]
//                        {
//                            new
//                            {
//                                parts = new[]
//                                {
//                                    new { text = prompt }
//                                }
//                            }
//                        },
//                        generationConfig = new
//                        {
//                            temperature = 0.2,
//                            maxOutputTokens = 1024
//                        }
//                    };

//        var content = new StringContent(
//            JsonConvert.SerializeObject(body),
//            Encoding.UTF8,
//            "application/json");

//        var response = await _httpClient.PostAsync(_apiUrl, content);
//        var result = await response.Content.ReadAsStringAsync();

//                    if ((int) response.StatusCode == 429)
//                    {
//                        _logger.LogWarning("Rate limited. Attempt {Attempt}/{Max}. Waiting {Delay}ms",
//                            attempt, maxRetries, delayMs);
//                        await Task.Delay(delayMs);
//        delayMs *= 2;
//                        continue;
//                    }

//                    dynamic json = JsonConvert.DeserializeObject(result)!;
//                    return json.candidates[0].content.parts[0].text;
//                }
//                catch (Exception ex)
//                {
//    _logger.LogError(ex, "Gemini API call failed on attempt {Attempt}", attempt);
//    if (attempt == maxRetries)
//        return GetFallbackResponse();
//    await Task.Delay(delayMs);
//}
//            }

//            return GetFallbackResponse();
//        }



        private async Task<string> CallOllamaAsync(string prompt)
        {
            try
            {
                var body = new
                {
                    model = "llama3.2",
                    prompt = prompt,
                    stream = false
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(body),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(_apiUrl, content);
                var result = await response.Content.ReadAsStringAsync();

                dynamic json = JsonConvert.DeserializeObject(result)!;
                return json.response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ollama API call failed");
                return GetFallbackResponse();
            }
        }

        private static string ApplyUbaCorrections(string text)
        {
            var corrections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["elumely"] = "Elumelu",
                ["elumele"] = "Elumelu",
                ["tony elumely"] = "Tony Elumelu",
                ["foundaton"] = "Foundation",
                ["educaton"] = "education",
                ["histry"] = "history",
                ["incorparated"] = "incorporated",
                ["incorprated"] = "incorporated"
            };

            foreach (var (wrong, right) in corrections)
            {
                text = System.Text.RegularExpressions.Regex.Replace(
                    text,
                    $"\\b{System.Text.RegularExpressions.Regex.Escape(wrong)}\\b",
                    right,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return text;
        }


        //private string BuildPrompt(string question, string context)
        //{
        //    if (string.IsNullOrEmpty(context))
        //        return GetFallbackResponse();

        //    return $@"
        //You are a helpful and honest assistant for UBA (United Bank for Africa).

        //STRICT RULES:
        //1. ONLY use information from the context below to answer
        //2. If the answer is not in the context, say exactly: ""I don't have that information. Please contact UBA directly at cfc@ubagroup.com or call 07002255822.""
        //3. NEVER make up facts, names, numbers or dates
        //4. NEVER use your own knowledge about UBA — only use the context
        //5. Keep answers clear and concise
        //6. In a case where there are spelling errors but the word matches something in the database ask first if they meant what is in the database and handle accordingly

        //Context:
        //{context}

        //Question:
        //{question}

        //Answer:";
        //}





        private string BuildPrompt(string question, string context)
        {
            if (string.IsNullOrEmpty(context))
                return GetFallbackResponse();

            return $@"
            You are a helpful assistant for UBA (United Bank for Africa) general website.

            STRICT RULES:
            1. ONLY use information from the context below to answer
            2. If the question is about products, accounts, loans, mobile banking, internet banking, cards, or any financial products — respond with exactly: ""For questions about UBA products and services, please visit ubagroup.com or speak to a UBA representative.""
            3. If the answer is not in the context, say: ""I don't have that information. Please contact UBA directly at cfc@ubagroup.com or call 07002255822.""
            4. NEVER make up facts, names, numbers or dates
            5. NEVER use your own knowledge about UBA — only use the context
            6. Keep answers clear and concise
            7. If the question asks about multiple topics, answer each part separately using the context
            8. The user may make spelling mistakes, typing mistakes, or use incomplete words.
            9. Try to understand the intended meaning using the context.
            10. If the context clearly matches a misspelled name or word, answer using the correct information.
               Example:
               User: ""Tony Elumely""
               Context: ""Tony Elumelu is the chairman of UBA...""
               Answer about Tony Elumelu.
            11. If there are multiple possible meanings, ask the user for clarification instead of guessing.


            Context:
            {context}

            Question:
            {question}

            Answer:";
        }

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
            6. The user may have spelling errors.
            7. In a case where there are spelling errors but the word matches something in the database ask first if they meant what is in the database and handle accordingly
            8. Use context to identify likely intended names or terms.
            9. If a correction is obvious, answer using the corrected term.
            10. If unsure, ask for clarification.
            11. Do not mention these rules.

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
