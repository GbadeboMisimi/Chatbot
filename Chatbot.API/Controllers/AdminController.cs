using Chatbot.API.Services.Implementation;
using Chatbot.API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "admin")]
    public class AdminController : ControllerBase
    {
        private readonly IScraperService _scraperService;
        private readonly IRetrievalService _retrievalService;
        private readonly IEmbeddingService _embeddingService;
        private readonly IConfiguration _configuration;

        public AdminController(IScraperService scraperService, IRetrievalService retrievalService, IEmbeddingService embeddingService, IConfiguration configuration)
        {
            _scraperService = scraperService;
            _retrievalService = retrievalService;
            _embeddingService = embeddingService;
            _configuration = configuration;
        }

        [HttpPost("scrape-all")]
        public async Task<IActionResult> ScrapeAll()
        {
            var result = await _scraperService.ScrapeAllAsync();
            return Ok(result);
        }

        [HttpPost("scrape-page")]
        public async Task<IActionResult> ScrapePage([FromBody] string url)
        {
            if (string.IsNullOrEmpty(url))
                return BadRequest("URL is required");

            var result = await _scraperService.ScrapePageAsync(url);
            return Ok(result);
        }

        [HttpGet("scraper-status")]
        public async Task<IActionResult> GetScraperStatus()
        {
            var status = await _scraperService.GetScraperStatusAsync();
            return Ok(status);
        }

        //[HttpGet("test-retrieval")]
        //public async Task<IActionResult> TestRetrieval([FromQuery] string query)
        //{
        //    var docs = await _retrievalService.GetRelevantDocumentsAsync(query);
        //    return Ok(new
        //    {
        //        Count = docs.Count(),
        //        Documents = docs.Select(d => new
        //        {
        //            d.Topic,
        //            d.Category,
        //            ContentPreview = d.Content.Substring(0, Math.Min(100, d.Content.Length))
        //        })
        //    });
        //}

        //[HttpGet("test-embedding")]
        //public async Task<IActionResult> TestEmbedding()
        //{
        //    var embedding = await _embeddingService.GetEmbeddingAsync("What is UBA?");
        //    if (embedding.Length == 0)
        //        return BadRequest("Embedding failed");
        //    return Ok($"Embedding successful - {embedding.Length} dimensions");
        //}

        //[HttpGet("list-models")]
        //public async Task<IActionResult> ListModels()
        //{
        //    using var client = new HttpClient();
        //    var apiKey = _configuration["GeminiSettings:ApiKey"];
        //    var response = await client.GetStringAsync(
        //        $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}");
        //    return Ok(response);
        //}


        //[HttpGet("test-scrape")]
        //public async Task<IActionResult> TestScrape()
        //{
        //    using var client = new HttpClient();
        //    client.Timeout = TimeSpan.FromSeconds(30);
        //    client.DefaultRequestHeaders.Add("User-Agent",
        //        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        //    try
        //    {
        //        var html = await client.GetStringAsync("https://www.ubagroup.com/about-us/who-we-are/");
        //        return Ok($"Success - got {html.Length} characters");
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest($"Failed: {ex.Message}");
        //    }
    }
    
}