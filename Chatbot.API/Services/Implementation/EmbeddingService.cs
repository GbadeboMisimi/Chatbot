using Chatbot.API.Services.Interface;
using Newtonsoft.Json;
using System.Text;

namespace Chatbot.API.Services.Implementation
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmbeddingService> _logger;
        private readonly string _apiKey;

        public EmbeddingService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<EmbeddingService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _logger = logger;
            _apiKey = _configuration["GeminiSettings:ApiKey"]!;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/embedding-001:embedContent?key={_apiKey}";

                var body = new
                {
                    model = "models/embedding-001",
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = text }
                        }
                    }
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(body),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();

                dynamic json = JsonConvert.DeserializeObject(result)!;
                var valuesArray = json.embedding.values;

                var embedding = new float[valuesArray.Count];
                for (int i = 0; i < valuesArray.Count; i++)
                {
                    embedding[i] = (float)valuesArray[i];
                }

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding");
                return Array.Empty<float>();
            }
        }

        public float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length || a.Length == 0)
                return 0;

            float dotProduct = 0;
            float magnitudeA = 0;
            float magnitudeB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return dotProduct / (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }
    }
}