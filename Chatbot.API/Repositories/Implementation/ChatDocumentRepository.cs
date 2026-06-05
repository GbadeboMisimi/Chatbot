using Chatbot.API.Core.Data;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.API.Repositories.Implementation
{
    public class ChatDocumentRepository : IChatDocumentRepository
    {
        private readonly AppDbContext _context;

        public ChatDocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ChatDocument>> GetAllAsync()
        {
            return await _context.ChatDocuments.ToListAsync();
        }

        public async Task<IEnumerable<ChatDocument>> GetByCategoryAsync(string category)
        {
            return await _context.ChatDocuments
                .Where(doc => doc.Category == category)
                .ToListAsync();
        }


        public async Task<IEnumerable<ChatDocument>> SearchAsync(string query)
        {
            var queryWords = query.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 1)
                .ToList();

            if (!queryWords.Any())
                return Enumerable.Empty<ChatDocument>();

            var allDocs = await _context.ChatDocuments.ToListAsync();

            return allDocs.Where(doc =>
                queryWords.Any(word =>
                    doc.Content.ToLower().Contains(word) ||
                    doc.Topic.ToLower().Contains(word) ||
                    doc.Category.ToLower().Contains(word)))
                .OrderByDescending(doc =>
                    queryWords.Count(word => doc.Content.ToLower().Contains(word)))
                .ToList();
        }



        //public async Task<IEnumerable<ChatDocument>> SearchAsync(string query)
        //{
        //    var queryWords = query.ToLower()
        //        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        //        .Where(w => w.Length > 2)
        //        .ToList();

        //    if (!queryWords.Any())
        //        return Enumerable.Empty<ChatDocument>();

        //    var allDocs = await _context.ChatDocuments.ToListAsync();

        //    return allDocs.Where(doc =>
        //        queryWords.Any(word =>
        //            doc.Content.ToLower().Contains(word) ||
        //            doc.Topic.ToLower().Contains(word) ||
        //            doc.Category.ToLower().Contains(word)));
        //}

        public async Task<DateTime?> GetLastScrapedDateAsync()
        {
            return await _context.ChatDocuments
                .OrderByDescending(d => d.LastScraped)
                .Select(d => (DateTime?)d.LastScraped)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<string>> GetScrapedUrlsAsync()
        {
            return await _context.ChatDocuments
                .Select(d => d.Url)
                .Distinct()
                .ToListAsync();
        }

        public async Task AddAsync(ChatDocument document)
        {
            await _context.ChatDocuments.AddAsync(document);
        }

        public async Task AddRangeAsync(IEnumerable<ChatDocument> documents)
        {
            await _context.ChatDocuments.AddRangeAsync(documents);
        }

        public async Task DeleteAllAsync()
        {
            var docs = await _context.ChatDocuments.ToListAsync();
            _context.ChatDocuments.RemoveRange(docs);
        }

        public async Task DeleteByUrlAsync(string url)
        {
            var docs = await _context.ChatDocuments
                .Where(d => d.Url == url)
                .ToListAsync();
            _context.ChatDocuments.RemoveRange(docs);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}