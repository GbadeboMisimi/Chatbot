using Chatbot.API.Core.Data;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.API.Repositories.Implementation
{
    public class ChatHistoryRepository : IChatHistoryRepository  
    {
        private readonly AppDbContext _context;

        public ChatHistoryRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<ChatHistory>> GetBySessionIdAsync(Guid sessionId)
        {
            return await _context.ChatHistories
                .Where(ch => ch.SessionId == sessionId)
                .OrderBy(ch => ch.SentAt)
                .ToListAsync();
        }
        public async Task<IEnumerable<ChatHistory>> GetByUserIdAsync(int userId)
        {
            return await _context.ChatHistories
                .Where(ch => ch.UserId == userId)
                .OrderBy(ch => ch.SentAt)
                .ToListAsync();
        }
        public async Task AddAsync(ChatHistory chatHistory)
        {
            await _context.ChatHistories.AddAsync(chatHistory);
        }
        public async Task DeleteSessionAsync(Guid sessionId)
        {
            var messages = await _context.ChatHistories
                .Where(ch => ch.SessionId == sessionId)
                .ToListAsync();

            _context.ChatHistories.RemoveRange(messages);
        }
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }

}


