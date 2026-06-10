using Chatbot.API.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.API.Core.Data
{
public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) 
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<ChatHistory> ChatHistories => Set<ChatHistory>();
        public DbSet<ChatDocument> ChatDocuments => Set<ChatDocument>();
        public DbSet<ChatSession> ChatSessions => Set<ChatSession>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(options =>
                    options.CommandTimeout(500));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20).HasDefaultValue("user"); 
                entity.Property(e => e.CreatedAt).IsRequired(); 

                entity.HasIndex(e => e.Email).IsUnique();

                entity.HasMany(e => e.ChatHistories)
                      .WithOne(c => c.User)
                      .HasForeignKey(c => c.UserId);
            });

            modelBuilder.Entity<ChatHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SessionId).IsRequired();
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Message).IsRequired();
                entity.Property(e => e.SentAt).IsRequired();

                entity.HasIndex(e => e.SessionId);
            });

            modelBuilder.Entity<ChatDocument>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Topic).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Embedding).IsRequired(false); 
                entity.Property(e => e.LastScraped).IsRequired();

                entity.HasIndex(e => e.Category);
            });

            modelBuilder.Entity<ChatSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SessionId).IsRequired();
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CreatedAt).IsRequired();

                entity.HasIndex(e => e.SessionId).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany(u => u.ChatSessions)
                      .HasForeignKey(e => e.UserId);

                entity.HasMany(e => e.ChatHistories)
                      .WithOne(c => c.ChatSession)
                      .HasForeignKey(c => c.ChatSessionId);
            });
        }
    }
}
