using Chatbot.API.Core.DTOs;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Chatbot.API.Services.Interface;
using FuzzySharp;
using Newtonsoft.Json;

namespace Chatbot.API.Services.Implementation
{
    public class RetrievalService : IRetrievalService
    {
        private readonly IChatDocumentRepository _documentRepository;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<RetrievalService> _logger;
 
        public RetrievalService(
            IChatDocumentRepository documentRepository,
            IEmbeddingService embeddingService,
            ILogger<RetrievalService> logger)
        {
            _documentRepository = documentRepository;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task<IEnumerable<ChatDocument>> GetRelevantDocumentsAsync(string query, int topK = 10)
        {
            // Generate embedding for the query
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);

            // Fall back to keyword search if embedding fails
            if (queryEmbedding.Length == 0)
            {
                _logger.LogWarning("Embedding failed, falling back to keyword search");
                return await FallbackKeywordSearch(query, topK);
            }

            //var allDocuments = await _documentRepository.GetAllAsync();
            var allDocuments = await _documentRepository.GetAllWithEmbeddingsAsync();

            // Score each document by cosine similarity
            var scoredDocuments = allDocuments
                .Where(doc => !string.IsNullOrEmpty(doc.Embedding))
                .Select(doc =>
                {
                    var docEmbedding = JsonConvert.DeserializeObject<float[]>(doc.Embedding!)!;
                    var similarity = _embeddingService.CosineSimilarity(queryEmbedding, docEmbedding);
                    return new { Document = doc, Score = similarity };
                })
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Document)
                .ToList();

            _logger.LogInformation(
                "Found {Count} relevant documents for query: {Query}",
                scoredDocuments.Count, query);

            if (scoredDocuments.Any())
            {
                return scoredDocuments;
            }

            _logger.LogInformation(
                "No embedding matches found. Trying fuzzy search.");

            return await FuzzySearchAsync(query, topK); ;
        }

        //public async Task<string> BuildContextAsync(string query)
        //{
        //    var documents = await GetRelevantDocumentsAsync(query);

        //    if (!documents.Any())
        //        return string.Empty;

        //    var context = new System.Text.StringBuilder();

        //    foreach (var doc in documents)
        //    {
        //        context.AppendLine($"[{doc.Topic}]");
        //        context.AppendLine(doc.Content);
        //        context.AppendLine();
        //    }

        //    return context.ToString().Trim();
        //}




        public async Task<string> BuildContextAsync(string query)
        {
            var documents = await GetRelevantDocumentsAsync(query);

            if (!documents.Any())
                return string.Empty;

            var context = new System.Text.StringBuilder();
            var seenContent = new HashSet<string>();

            foreach (var doc in documents)
            {
                // Skip duplicate content
                if (seenContent.Contains(doc.Content))
                    continue;

                seenContent.Add(doc.Content);
                context.AppendLine($"[{doc.Topic}]");
                context.AppendLine(doc.Content);
                context.AppendLine();
            }

            return context.ToString().Trim();
        }

        public async Task<(string Context, List<CitationDto> Citations)> BuildContextWithCitationsAsync(string query)
        {
            var documents = await GetRelevantDocumentsAsync(query);

            if (!documents.Any())
                return (string.Empty, new List<CitationDto>());

            var context = new System.Text.StringBuilder();
            var seenContent = new HashSet<string>();
            var seenUrls = new HashSet<string>();
            var citations = new List<CitationDto>();

            foreach (var doc in documents)
            {
                if (seenContent.Contains(doc.Content))
                    continue;

                seenContent.Add(doc.Content);
                context.AppendLine($"[{doc.Topic}]");
                context.AppendLine(doc.Content);
                context.AppendLine();

                // Add citation only once per URL
                if (!seenUrls.Contains(doc.Url))
                {
                    seenUrls.Add(doc.Url);
                    citations.Add(new CitationDto
                    {
                        Topic = doc.Topic,
                        Url = doc.Url,
                        Category = doc.Category
                    });
                }
            }

            return (context.ToString().Trim(), citations);
        }

        public async Task<IEnumerable<ChatDocument>> GetByCategoryAsync(string category, int topK = 5)
        {
            var documents = await _documentRepository.GetByCategoryAsync(category);
            return documents.Take(topK).ToList();
        }

        public async Task<bool> HasRelevantDocumentsAsync(string query)
        {
            var documents = await GetRelevantDocumentsAsync(query, topK: 3);
            return documents.Any();
        }

        private async Task<IEnumerable<ChatDocument>> FallbackKeywordSearch(string query, int topK)
        {
            var queryWords = query.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 1)
                .ToList();

            var allDocuments = await _documentRepository.GetAllAsync();

            return allDocuments
                .Select(doc => new
                {
                    Document = doc,
                    Score =
                        (doc.Content.ToLower().Contains(query.ToLower()) ? 10 : 0) +
                        queryWords.Count(w => doc.Content.ToLower().Contains(w)) * 2 +
                        queryWords.Count(w => doc.Topic.ToLower().Contains(w))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Document)
                .ToList();
        }

        private async Task<IEnumerable<ChatDocument>> FuzzySearchAsync(string query, int topK)
        {
            var allDocuments = await _documentRepository.GetAllAsync();

            var matches = allDocuments
                .Select(doc => new
                {
                    Document = doc,

                    Score = Math.Max(
                        Fuzz.PartialRatio(query, doc.Topic),
                        Math.Max(
                            Fuzz.PartialRatio(query, doc.Category),
                            Fuzz.PartialRatio(
                                query,
                                doc.Content.Length > 300
                                    ? doc.Content.Substring(0, 300)
                                    : doc.Content)
                        )
                    )
                })
                .Where(x => x.Score >= 80)
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Document)
                .ToList();

            return matches;
        }

        //private async Task<string?> FindClosestTopicAsync(string query)
        //{
        //    var docs = await _documentRepository.GetAllAsync();

        //    var bestMatch = docs
        //        .Select(d => new
        //        {
        //            Topic = d.Topic,
        //            Score = Fuzz.PartialRatio(query, d.Topic)
        //        })
        //        .OrderByDescending(x => x.Score)
        //        .FirstOrDefault();

        //    if (bestMatch != null && bestMatch.Score >= 85)
        //        return bestMatch.Topic;

        //    return null;
        //}
    }
}