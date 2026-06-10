using Chatbot.API.Core.Data;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.API.Repositories.Implementation
{
    public class ChatSessionRepository : IChatSessionRepository
    {
        private readonly AppDbContext _context;

        public ChatSessionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ChatSession?> GetBySessionIdAsync(Guid sessionId)
        {
            return await _context.ChatSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        }

        public async Task<IEnumerable<ChatSession>> GetByUserIdAsync(int userId)
        {
            return await _context.ChatSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.LastMessageAt ?? s.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(ChatSession session)
        {
            await _context.ChatSessions.AddAsync(session);
        }

        public void Update(ChatSession session)
        {
            _context.ChatSessions.Update(session);
        }

        public async Task UpdateAsync(ChatSession session)
        {
            _context.ChatSessions.Update(session);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(Guid sessionId)
        {
            var session = await GetBySessionIdAsync(sessionId);
            if (session != null)
                _context.ChatSessions.Remove(session);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}