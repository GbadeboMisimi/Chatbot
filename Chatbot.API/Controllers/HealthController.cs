using Chatbot.API.Core.Data;
using Chatbot.API.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.API.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAiService _aiService;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            AppDbContext context,
            IAiService aiService,
            IEmbeddingService embeddingService,
            ILogger<HealthController> logger)
        {
            _context = context;
            _aiService = aiService;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Check()
        {
            var dbStatus = "Disconnected";
            var llmStatus = "Unavailable";
            var embeddingStatus = "Unavailable";
            var overallStatus = "Unhealthy";

            // Check database
            try
            {
                await _context.Database.CanConnectAsync();
                dbStatus = "Connected";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
            }

            // Check AI service (Ollama or Gemini)
            try
            {
                var isAvailable = await _aiService.IsAvailableAsync();
                llmStatus = isAvailable ? "Running" : "Unavailable";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI service health check failed");
            }

            // Check embedding service
            try
            {
                var testEmbedding = await _embeddingService.GetEmbeddingAsync("test");
                embeddingStatus = testEmbedding.Length > 0 ? "Available" : "Unavailable";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Embedding service health check failed");
            }

            // Overall status
            if (dbStatus == "Connected" &&
               llmStatus == "Running" &&
                embeddingStatus == "Available")
            {
                overallStatus = "Healthy";
            }
            else if (dbStatus == "Connected")
            {
                overallStatus = "Degraded";
            }

            var statusCode = overallStatus == "Healthy" ? 200 :
                             overallStatus == "Degraded" ? 200 : 503;

            return StatusCode(statusCode, new
            {
                Status = overallStatus,
                Database = dbStatus,
                llm = llmStatus,
                Embeddings = embeddingStatus,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}