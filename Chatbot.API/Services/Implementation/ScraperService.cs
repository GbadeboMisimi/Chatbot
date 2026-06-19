using Chatbot.API.Core.Data;
using Chatbot.API.Core.DTOs;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Chatbot.API.Services.Interface;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace Chatbot.API.Services.Implementation
{
    public class ScraperService : IScraperService
    {
        private readonly IChatDocumentRepository _documentRepository;
        private readonly ILogger<ScraperService> _logger;
        private readonly IEmbeddingService _embeddingService;

        public ScraperService(
            IChatDocumentRepository documentRepository,
            ILogger<ScraperService> logger,
            IEmbeddingService embeddingService)
        {
            _documentRepository = documentRepository;
            _logger = logger;
            _embeddingService = embeddingService;
        }

        public async Task<string> ScrapeAllAsync()
        {
            var documents = new List<ChatDocument>();
            var successCount = 0;
            var failCount = 0;

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            });

            foreach (var url in SeedUrls.Urls)
            {
                try
                {
                    _logger.LogInformation("Scraping: {Url}", url);

                    var page = await context.NewPageAsync();
                    await page.GotoAsync(url.ToString(), new()
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 30000
                    });

                    await page.WaitForTimeoutAsync(2000);

                    // Extract text from page
                    var texts = await page.EvaluateAsync<string[]>(@"
                        () => {
                            const elements = document.querySelectorAll('p, h1, h2, h3, li, article');
                            return Array.from(elements)
                                .map(el => el.innerText.trim())
                                .filter(text => text.length > 40);
                        }
                    ");

                    await page.CloseAsync();

                    if (texts == null || texts.Length == 0)
                    {
                        _logger.LogWarning("No content found at: {Url}", url);
                        failCount++;
                        continue;
                    }

                    var chunks = ChunkContent(texts.ToList());

                    foreach (var chunk in chunks)
                    {
                        var embedding = await _embeddingService.GetEmbeddingAsync(chunk);
                        var embeddingJson = JsonConvert.SerializeObject(embedding);

                        documents.Add(new ChatDocument
                        {
                            Url = url,
                            Topic = GetTopicFromUrl(url),
                            Category = SeedUrls.GetCategoryFromUrl(url),
                            Content = chunk,
                            Embedding = embeddingJson,
                            LastScraped = DateTime.UtcNow
                        });

                        await Task.Delay(500);
                    }

                    successCount++;
                    _logger.LogInformation("Scraped {Count} chunks from: {Url}", chunks.Count, url);

                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to scrape: {Url} - Error: {Message}", url, ex.Message);
                    failCount++;
                }
            }

            await _documentRepository.DeleteAllAsync();
            await _documentRepository.SaveChangesAsync();

            // Save in batches of 50
            var batchSize = 50;
            for (int i = 0; i < documents.Count; i += batchSize)
            {
                var batch = documents.Skip(i).Take(batchSize).ToList();
                await _documentRepository.AddRangeAsync(batch);
                await _documentRepository.SaveChangesAsync();
                _logger.LogInformation("Saved batch {Batch} of {Total}",
                    (i / batchSize) + 1,
                    (documents.Count / batchSize) + 1);
            }

            //await _documentRepository.DeleteAllAsync();
            //await _documentRepository.AddRangeAsync(documents);
            //await _documentRepository.SaveChangesAsync();

            return $"Scraping complete. Success: {successCount} pages, Failed: {failCount} pages, Total chunks: {documents.Count}";
        }

        public async Task<string> ScrapePageAsync(string url)
        {
            if (!SeedUrls.Urls.Contains(url))
                return "URL not in allowed list";

            try
            {
                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

                var page = await browser.NewPageAsync();
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 60000
                });

                await page.WaitForTimeoutAsync(2000);

                var texts = await page.EvaluateAsync<string[]>(@"
                    () => {
                        const elements = document.querySelectorAll('p, h1, h2, h3, li, article');
                        return Array.from(elements)
                            .map(el => el.innerText.trim())
                            .filter(text => text.length > 40);
                    }
                ");

                await page.CloseAsync();

                if (texts == null || texts.Length == 0)
                    return "No content found at this URL";

                var chunks = ChunkContent(texts.ToList());

                await _documentRepository.DeleteByUrlAsync(url);

                var documents = new List<ChatDocument>();

                foreach (var chunk in chunks)
                {
                    var embedding = await _embeddingService.GetEmbeddingAsync(chunk);
                    var embeddingJson = JsonConvert.SerializeObject(embedding);

                    documents.Add(new ChatDocument
                    {
                        Url = url,
                        Topic = GetTopicFromUrl(url),
                        Category = SeedUrls.GetCategoryFromUrl(url),
                        Content = chunk,
                        Embedding = embeddingJson,
                        LastScraped = DateTime.UtcNow
                    });

                    await Task.Delay(500);
                }

                await _documentRepository.AddRangeAsync(documents);
                await _documentRepository.SaveChangesAsync();

                return $"Successfully scraped {documents.Count} chunks from {url}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scrape: {Url}", url);
                return $"Failed to scrape {url}: {ex.Message}";
            }
        }

        public async Task<ScraperStatusDto> GetScraperStatusAsync()
        {
            var allDocs = await _documentRepository.GetAllAsync();
            var docList = allDocs.ToList();
            var scrapedUrls = await _documentRepository.GetScrapedUrlsAsync();
            var lastScraped = await _documentRepository.GetLastScrapedDateAsync();

            return new ScraperStatusDto
            {
                TotalDocuments = docList.Count,
                LastScraped = lastScraped,
                ScrapedUrls = scrapedUrls.ToList(),
                TotalUrlsConfigured = SeedUrls.Urls.Count,
                TotalUrlsScraped = scrapedUrls.Count()
            };
        }

        private List<string> ChunkContent(List<string> paragraphs, int maxLength = 500)
        {
            var chunks = new List<string>();
            var current = new System.Text.StringBuilder();

            foreach (var paragraph in paragraphs)
            {
                if (current.Length + paragraph.Length > maxLength)
                {
                    if (current.Length > 0)
                    {
                        chunks.Add(current.ToString().Trim());
                        current.Clear();
                    }
                }
                current.AppendLine(paragraph);
            }

            if (current.Length > 0)
                chunks.Add(current.ToString().Trim());

            return chunks;
        }

        private string GetTopicFromUrl(string url)
        {
            var parts = url.TrimEnd('/').Split('/');
            var last = parts.Last();
            var words = last.Split('-');
            var topic = string.Join(" ", words);
            return char.ToUpper(topic[0]) + topic.Substring(1);
        }
    }
}































































//using Chatbot.API.Core.Data;
//using Chatbot.API.Core.DTOs;
//using Chatbot.API.Core.Models;
//using Chatbot.API.Repositories.Interface;
//using Chatbot.API.Services.Implementation;
//using Chatbot.API.Services.Interface;
//using HtmlAgilityPack;
//using Newtonsoft.Json;

//namespace Chatbot.API.Services.Implementation
//{
//    public class ScraperService : IScraperService
//    {
//        private readonly IChatDocumentRepository _documentRepository;
//        private readonly IEmbeddingService _embeddingService;
//        private readonly ILogger<ScraperService> _logger;

//        public ScraperService(
//            IChatDocumentRepository documentRepository,
//            IEmbeddingService embeddingService,
//            ILogger<ScraperService> logger)
//        {
//            _documentRepository = documentRepository;
//            _embeddingService = embeddingService;
//            _logger = logger;
//        }

//        public async Task<string> ScrapeAllAsync()
//        {
//            var documents = new List<ChatDocument>();
//            var successCount = 0;
//            var failCount = 0;

//            //foreach (var url in SeedUrls.Urls)
//            //{
//            //    try
//            //    {
//            //        var chunks = await ExtractContentAsync(url);
//            //        // ... rest of your code ...

//            //        // Add delay between requests
//            //        await Task.Delay(1000); // 1 second between each page
//            //    }
//            //    catch (Exception ex)
//            //    {
//            //        _logger.LogError(ex, "Failed to scrape: {Url}", url);
//            //        failCount++;
//            //    }
//            //}

//            foreach (var url in SeedUrls.Urls)
//            {
//                try
//                {
//                    var chunks = await ExtractContentAsync(url);

//                    if (!chunks.Any())
//                    {
//                        _logger.LogWarning("No content found at: {Url}", url);
//                        failCount++;
//                        continue;
//                    }

//                    foreach (var chunk in chunks)
//                    {
//                        // Generate embedding for each chunk
//                        var embedding = await _embeddingService.GetEmbeddingAsync(chunk);
//                        var embeddingJson = JsonConvert.SerializeObject(embedding);

//                        documents.Add(new ChatDocument
//                        {
//                            Url = url,
//                            Topic = GetTopicFromUrl(url),
//                            Category = SeedUrls.GetCategoryFromUrl(url),
//                            Content = chunk,
//                            Embedding = embeddingJson,
//                            LastScraped = DateTime.UtcNow
//                        });

//                        // Small delay to avoid hitting Gemini rate limits
//                        await Task.Delay(1000);
//                    }

//                    _logger.LogInformation("Scraped {Count} chunks from: {Url}", chunks.Count, url);
//            }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Failed to scrape: {Url} - Error: {Message}", url, ex.Message);
//                    failCount++;
//                }
//            }

//        await _documentRepository.DeleteAllAsync();
//            await _documentRepository.AddRangeAsync(documents);
//            await _documentRepository.SaveChangesAsync();

//            return $"Scraping complete. Success: {successCount} pages, Failed: {failCount} pages, Total chunks: {documents.Count}";
//        }

//        public async Task<string> ScrapePageAsync(string url)
//        {
//            if (!SeedUrls.Urls.Contains(url))
//                return "URL not in allowed list";

//            try
//            {
//                var chunks = await ExtractContentAsync(url);

//                if (!chunks.Any())
//                    return "No content found at this URL";

//                // Delete old chunks for this URL only
//                await _documentRepository.DeleteByUrlAsync(url);

//                var documents = chunks.Select(chunk => new ChatDocument
//                {
//                    Url = url,
//                    Topic = GetTopicFromUrl(url),
//                    Category = SeedUrls.GetCategoryFromUrl(url),
//                    Content = chunk,
//                    LastScraped = DateTime.UtcNow
//                }).ToList();

//                await _documentRepository.AddRangeAsync(documents);
//                await _documentRepository.SaveChangesAsync();

//                return $"Successfully scraped {documents.Count} chunks from {url}";
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to scrape: {Url}", url);
//                return $"Failed to scrape {url}: {ex.Message}";
//            }
//        }

//        public async Task<ScraperStatusDto> GetScraperStatusAsync()
//        {
//            var allDocs = await _documentRepository.GetAllAsync();
//            var docList = allDocs.ToList();
//            var scrapedUrls = await _documentRepository.GetScrapedUrlsAsync();
//            var lastScraped = await _documentRepository.GetLastScrapedDateAsync();

//            return new ScraperStatusDto
//            {
//                TotalDocuments = docList.Count,
//                LastScraped = lastScraped,
//                ScrapedUrls = scrapedUrls.ToList(),
//                TotalUrlsConfigured = SeedUrls.Urls.Count,
//                TotalUrlsScraped = scrapedUrls.Count()
//            };
//        }

//        private async Task<List<string>> ExtractContentAsync(string url)
//        {
//            var web = new HtmlWeb();

//            // Make request look like a real browser
//            web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

//            // Set timeout to 30 seconds
//            web.PreRequest = request =>
//            {
//                request.Timeout = 30000;
//                return true;
//            };

//            var doc = await Task.Run(() => web.Load(url));

//            // Remove scripts, styles, nav, footer
//            var removeNodes = doc.DocumentNode
//                .Descendants()
//                .Where(n => n.Name is "script" or "style" or "nav" or "footer" or "header")
//                .ToList();

//            foreach (var node in removeNodes)
//                node.Remove();

//            // Extract from multiple element types
//            var texts = new List<string>();
//            var nodes = doc.DocumentNode.SelectNodes("//p | //li | //h1 | //h2 | //h3 | //article");

//            if (nodes != null)
//            {
//                foreach (var node in nodes)
//                {
//                    var text = node.InnerText.Trim();
//                    if (text.Length > 40)
//                        texts.Add(text);
//                }
//            }

//            return ChunkContent(texts);
//        }

//        private List<string> ChunkContent(List<string> paragraphs)
//        {
//            var chunks = new List<string>();
//            var current = new System.Text.StringBuilder();

//            foreach (var paragraph in paragraphs)
//            {
//                if (current.Length + paragraph.Length > 500)
//                {
//                    if (current.Length > 0)
//                    {
//                        chunks.Add(current.ToString().Trim());
//                        current.Clear();
//                    }
//                }
//                current.AppendLine(paragraph);
//            }

//            if (current.Length > 0)
//                chunks.Add(current.ToString().Trim());

//            return chunks;
//        }

//        private string GetTopicFromUrl(string url)
//        {
//            var parts = url.TrimEnd('/').Split('/');
//            var last = parts.Last();
//            var words = last.Split('-');
//            var topic = string.Join(" ", words);
//            return char.ToUpper(topic[0]) + topic.Substring(1);
//        }
//    }
//}


////foreach (var chunk in chunks)
////{
////    // Generate embedding for each chunk
////    var embedding = await _embeddingService.GetEmbeddingAsync(chunk);
////    var embeddingJson = JsonConvert.SerializeObject(embedding);

////    documents.Add(new ChatDocument
////    {
////        Url = url,
////        Topic = GetTopicFromUrl(url),
////        Category = SeedUrls.GetCategoryFromUrl(url),
////        Content = chunk,
////        Embedding = embeddingJson,
////        LastScraped = DateTime.UtcNow
////    });

////    // Small delay to avoid hitting Gemini rate limits
////    await Task.Delay(200);
////}