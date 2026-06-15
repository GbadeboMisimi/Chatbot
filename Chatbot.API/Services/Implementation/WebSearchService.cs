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
                var candidates = await GetCandidateUrlsAsync(query);
                var queryIntent = GetQueryIntent(query);

                foreach (var url in candidates.Take(8))
                {
                    var content = await ScrapeUbaPageAsync(url);
                    if (string.IsNullOrWhiteSpace(content))
                        continue;

                    if (LooksRelevant(query, content) || IsIntentMatch(url, queryIntent))
                        return (content, url.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UBA live website fallback failed for query: {Query}", query);
            }

            return null;
        }

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

            if (urls.Count == 0)
                urls.Add(BaseUri);

            var queryTerms = GetSignificantTerms(query).ToList();
            var queryIntent = GetQueryIntent(query);

            return urls
                .OrderByDescending(uri => ScoreUrl(uri, queryTerms, queryIntent))
                .ThenBy(uri => uri.AbsoluteUri.Length)
                .Take(20)
                .ToList();
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
                _logger.LogWarning("Blocked non-UBA URL during live fallback: {Url}", url);
                return null;
            }

            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            foreach (var node in doc.DocumentNode
                .Descendants()
                .Where(n => n.Name is "script" or "style" or "nav" or "footer" or "header" or "noscript")
                .ToList())
            {
                node.Remove();
            }

            var texts = doc.DocumentNode
                .SelectNodes("//main//h1 | //main//h2 | //main//h3 | //main//p | //main//li | //article//h1 | //article//h2 | //article//h3 | //article//p | //article//li | //h1 | //h2 | //h3 | //p | //li")
                ?.Select(node => WebUtility.HtmlDecode(node.InnerText).Trim())
                .Select(text => Regex.Replace(text, "\\s+", " "))
                .Where(text => text.Length > 10)
                .Distinct()
                .Take(60)
                .ToList();

            if (texts == null || texts.Count == 0)
                return null;

            return string.Join(Environment.NewLine, texts);
        }

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
