using Chatbot.API.Services.Interface;
using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Chatbot.API.Services.Implementation
{
    public class WebSearchService : IWebSearchService
    {
        private static readonly Uri BaseUri = new("https://www.ubagroup.com/");
        private static readonly Uri SitemapUri = new("https://www.ubagroup.com/sitemap.xml");
        private static readonly string[] BlockedPathTerms =
        {
            "account", "accounts", "card", "cards", "loan", "loans", "banking",
            "mobile-banking", "internet-banking", "ussd", "transfer", "payments",
            "/category/", "/tag/", "/author/"
        };

        private readonly HttpClient _httpClient;
        private readonly ILogger<WebSearchService> _logger;

        public WebSearchService(IHttpClientFactory httpClientFactory, ILogger<WebSearchService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _logger = logger;
        }




        public async Task<(string Content, string Url)?> SearchUbaWebsiteAsync(string query)
        {
            try
            {
                _logger.LogInformation(
                    "Searching UBA website for query: {Query}",
                    query);

                var candidates = await GetCandidateUrlsAsync(query);

                _logger.LogInformation(
                    "Candidate URLs found: {Count}",
                    candidates.Count);

                var queryIntent = GetQueryIntent(query);

                foreach (var url in candidates.Take(8))
                {
                    _logger.LogInformation(
                        "Checking URL: {Url}",
                        url);

                    var content = await ScrapeUbaPageAsync(url);
                    _logger.LogInformation("Scraped content length from {Url}: {Length}", url, content?.Length ?? 0);

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        _logger.LogInformation(
                            "No content found for URL: {Url}",
                            url);

                        continue;
                    }

                    if (LooksRelevant(query, content) || IsIntentMatch(url, queryIntent))
                    {
                        _logger.LogInformation(
                            "Relevant page found: {Url}",
                            url);

                        return (content, url.ToString());
                    }

                    _logger.LogInformation(
                        "Page not relevant: {Url}",
                        url);
                }

                _logger.LogInformation(
                    "No matching page found for query: {Query}",
                    query);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "UBA live website fallback failed for query: {Query}",
                    query);
            }

            return null;
        }








        //public async Task<(string Content, string Url)?> SearchUbaWebsiteAsync(string query)
        //{
        //    try
        //    {
        //        var candidates = await GetCandidateUrlsAsync(query);
        //        var queryIntent = GetQueryIntent(query);

        //        foreach (var url in candidates.Take(8))
        //        {
        //            var content = await ScrapeUbaPageAsync(url);
        //            if (string.IsNullOrWhiteSpace(content))
        //                continue;

        //            if (LooksRelevant(query, content) || IsIntentMatch(url, queryIntent))
        //                return (content, url.ToString());
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "UBA live website fallback failed for query: {Query}", query);
        //    }

        //    return null;
        //}

        private async Task<List<Uri>> GetCandidateUrlsAsync(string query)
        {
            var urls = new List<Uri>();

            try
            {
                urls = await ReadSitemapUrlsAsync(SitemapUri);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read UBA sitemap. Falling back to homepage candidates.");
            }

            // If sitemap provided no urls, or reading failed, fall back to crawling the homepage for candidates
            if (urls.Count == 0)
            {
                _logger.LogInformation("No sitemap urls found, crawling homepage for candidates");
                try
                {
                    var crawled = await CrawlHomepageAsync(BaseUri, maxLinks: 200);
                    if (crawled.Any())
                        urls.AddRange(crawled);
                    else
                        urls.Add(BaseUri);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Homepage crawl failed, using base uri as candidate");
                    urls.Add(BaseUri);
                }
            }

            var queryTerms = GetSignificantTerms(query).ToList();
            var queryIntent = GetQueryIntent(query);

            // Deduplicate and filter product/service pages early
            var candidates = urls
                .OrderByDescending(uri => ScoreUrl(uri, queryTerms, queryIntent))
                .ThenBy(uri => uri.AbsoluteUri.Length)
                .Where(uri => !IsProductOrServicePath(uri))
                .DistinctBy(uri => uri.AbsoluteUri)
                .Take(50)
                .ToList();

            return candidates;
        }

        private async Task<List<Uri>> ReadSitemapUrlsAsync(Uri sitemapUri)
        {
            var xml = await _httpClient.GetStringAsync(sitemapUri);
            var sitemap = XDocument.Parse(xml);
            XNamespace ns = sitemap.Root?.Name.Namespace ?? XNamespace.None;
            var locs = sitemap.Descendants(ns + "loc")
                .Select(node => node.Value.Trim())
                .Select(CreateAllowedUri)
                .Where(uri => uri != null)
                .Select(uri => uri!)
                .ToList();

            var isSitemapIndex = sitemap.Root?.Name.LocalName.Equals("sitemapindex", StringComparison.OrdinalIgnoreCase) == true;
            if (!isSitemapIndex)
            {
                return locs
                    .Where(uri => !IsProductOrServicePath(uri))
                    .DistinctBy(uri => uri.AbsoluteUri)
                    .ToList();
            }

            var pageUrls = new List<Uri>();
            foreach (var childSitemap in locs.Where(uri => uri.AbsolutePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).Take(12))
            {
                try
                {
                    pageUrls.AddRange(await ReadSitemapUrlsAsync(childSitemap));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read child UBA sitemap: {Sitemap}", childSitemap);
                }
            }

            return pageUrls
                .Where(uri => !IsProductOrServicePath(uri))
                .DistinctBy(uri => uri.AbsoluteUri)
                .ToList();
        }



        private async Task<string?> ScrapeUbaPageAsync(Uri url)
        {
            if (!IsAllowedUbaUri(url))
            {
                _logger.LogWarning(
                    "Blocked non-UBA URL during live fallback: {Url}",
                    url);

                return null;
            }

            string html;
            try
            {
                html = await _httpClient.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download {Url}", url);
                return null;
            }

            _logger.LogInformation("Downloaded {Length} characters from {Url}", html.Length, url);

            var doc = new HtmlDocument();
            try
            {
                doc.LoadHtml(html);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse HTML for {Url}", url);
                return null;
            }

            foreach (var node in doc.DocumentNode
                .Descendants()
                .Where(n => n.Name is "script" or "style" or "nav" or "footer" or "header" or "noscript")
                .ToList())
            {
                node.Remove();
            }

            // broaden node selection to capture more content including divs and spans used by many sites
            var nodes = doc.DocumentNode.SelectNodes(
                "//main//h1 | //main//h2 | //main//h3 | //main//h4 | //main//h5 | //main//h6 | //main//p | //main//li | " +
                "//article//h1 | //article//h2 | //article//h3 | //article//h4 | //article//h5 | //article//h6 | //article//p | //article//li | " +
                "//h1 | //h2 | //h3 | //h4 | //h5 | //h6 | //p | //li | //div | //span | //td | //caption");

            _logger.LogInformation(
                "Found {Count} candidate nodes for {Url}",
                nodes?.Count ?? 0,
                url);

            if (nodes == null || nodes.Count == 0)
            {
                _logger.LogWarning("No textual nodes found for {Url}", url);
                return null;
            }

            // Preserve order and avoid aggressive deduplication so we don't lose lines
            var texts = nodes
                .Select(node => WebUtility.HtmlDecode(node.InnerText ?? string.Empty).Trim())
                .Select(text => Regex.Replace(text, "\\s+", " "))
                // allow shorter but meaningful lines; filter out extremely short noise
                .Where(text => text.Length > 2)
                // keep original ordering; increase limit to capture more content
                .Take(200)
                .ToList();

            _logger.LogInformation("Extracted {Count} text nodes from {Url}", texts.Count, url);
            if (texts.Any())
                _logger.LogInformation("First extracted text from {Url}: {Text}", url, texts.First());

            if (texts.Count == 0)
                return null;

            return string.Join(Environment.NewLine, texts);
        }

        private async Task<IEnumerable<Uri>> CrawlHomepageAsync(Uri startUri, int maxLinks = 100)
        {
            var results = new List<Uri>();
            try
            {
                var html = await _httpClient.GetStringAsync(startUri);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var anchors = doc.DocumentNode.SelectNodes("//a[@href]") ?? new HtmlNodeCollection(doc.DocumentNode);

                foreach (var a in anchors)
                {
                    var href = a.GetAttributeValue("href", string.Empty).Trim();
                    if (string.IsNullOrEmpty(href)) continue;

                    // Normalize relative urls
                    if (Uri.TryCreate(startUri, href, out var uri))
                    {
                        if (IsAllowedUbaUri(uri) && !IsProductOrServicePath(uri))
                        {
                            results.Add(uri);
                        }
                    }

                    if (results.Count >= maxLinks) break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Homepage crawl failed for {Url}", startUri);
            }

            return results.DistinctBy(u => u.AbsoluteUri).ToList();
        }


















        //private async Task<string?> ScrapeUbaPageAsync(Uri url)
        //{
        //    if (!IsAllowedUbaUri(url))
        //    {
        //        _logger.LogWarning("Blocked non-UBA URL during live fallback: {Url}", url);
        //        return null;
        //    }

        //    var html = await _httpClient.GetStringAsync(url);
        //    _logger.LogInformation("Downloaded {Length} characters from {Url}", html.Length, url);

        //    var doc = new HtmlDocument();
        //    doc.LoadHtml(html);

        //    foreach (var node in doc.DocumentNode
        //        .Descendants()
        //        .Where(n => n.Name is "script" or "style" or "nav" or "footer" or "header" or "noscript")
        //        .ToList())
        //    {
        //        node.Remove();
        //    }

        //    var texts = doc.DocumentNode
        //        .SelectNodes("//main//h1 | //main//h2 | //main//h3 | //main//p | //main//li | //article//h1 | //article//h2 | //article//h3 | //article//p | //article//li | //h1 | //h2 | //h3 | //p | //li")
        //        ?.Select(node => WebUtility.HtmlDecode(node.InnerText).Trim())
        //        .Select(text => Regex.Replace(text, "\\s+", " "))
        //        .Where(text => text.Length > 10)
        //        .Distinct()
        //        .Take(60)
        //        .ToList();
        //    if (texts == null || texts.Count == 0)
        //        return null;

        //    return string.Join(Environment.NewLine, texts);
        //}

        private static Uri? CreateAllowedUri(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return null;

            return IsAllowedUbaUri(uri) ? uri : null;
        }

        private static bool IsAllowedUbaUri(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttps &&
                   (uri.Host.Equals("ubagroup.com", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith(".ubagroup.com", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsProductOrServicePath(Uri uri)
        {
            var path = uri.AbsolutePath.ToLowerInvariant();
            return BlockedPathTerms.Any(term => path.Contains(term));
        }

        private static int ScoreUrl(Uri uri, IReadOnlyCollection<string> terms, QueryIntent intent)
        {
            var path = WebUtility.UrlDecode(uri.AbsolutePath).ToLowerInvariant();
            var score = terms.Count(term => path.Contains(term));

            if (path.Contains("about")) score += 2;
            if (path.Contains("who-we-are")) score += 2;
            if (path.Contains("leadership")) score += 2;
            if (path.Contains("foundation")) score += 2;
            if (path.Contains("history")) score += 2;
            if (path.Contains("/category/")) score -= 10;
            if (path.Contains("/tag/")) score -= 10;
            if (intent == QueryIntent.Leadership && path.Contains("leadership")) score += 20;
            if (intent == QueryIntent.Leadership && path.Contains("about")) score += 5;
            if (intent == QueryIntent.Foundation && path.Contains("foundation")) score += 20;
            if (intent == QueryIntent.History && (path.Contains("history") || path.Contains("who-we-are"))) score += 15;
            if (IsProductOrServicePath(uri)) score -= 20;

            return score;
        }

        private static bool LooksRelevant(string query, string content)
        {
            var terms = GetSignificantTerms(query).ToList();
            if (terms.Count == 0)
                return true;

            var lowerContent = content.ToLowerInvariant();
            return terms.Any(term => lowerContent.Contains(term));
        }

        private static bool IsIntentMatch(Uri uri, QueryIntent intent)
        {
            var path = WebUtility.UrlDecode(uri.AbsolutePath).ToLowerInvariant();
            return intent switch
            {
                QueryIntent.Leadership => path.Contains("leadership"),
                QueryIntent.Foundation => path.Contains("foundation"),
                QueryIntent.History => path.Contains("history") || path.Contains("who-we-are"),
                _ => false
            };
        }

        private static IEnumerable<string> GetSignificantTerms(string text)
        {
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "what", "when", "where", "who", "how", "why", "tell", "about",
                "does", "is", "are", "the", "and", "or", "of", "to", "in", "for",
                "with", "uba", "united", "bank", "africa"
            };

            return Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]+")
                .Select(match => match.Value)
                .Where(term => term.Length > 2 && !stopWords.Contains(term))
                .Distinct();
        }

        private static QueryIntent GetQueryIntent(string query)
        {
            var lower = query.ToLowerInvariant();
            if (lower.Contains("ceo") ||
                lower.Contains("chief executive") ||
                lower.Contains("managing director") ||
                lower.Contains("chairman") ||
                lower.Contains("leader") ||
                lower.Contains("leadership") ||
                lower.Contains("executive"))
                return QueryIntent.Leadership;

            if (lower.Contains("foundation") || lower.Contains("education") || lower.Contains("impact"))
                return QueryIntent.Foundation;

            if (lower.Contains("history") || lower.Contains("founded") || lower.Contains("incorporated"))
                return QueryIntent.History;

            return QueryIntent.General;
        }

        private enum QueryIntent
        {
            General,
            Leadership,
            Foundation,
            History
        }
    }
}
