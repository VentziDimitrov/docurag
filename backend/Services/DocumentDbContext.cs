using Microsoft.EntityFrameworkCore;
using backend.Models; 

namespace backend.Services;

public class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options)
    {
    }

    public DbSet<ConversationMessage> Conversations { get; set; }
    public DbSet<Models.Document> Documents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConversationMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConversationId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DocumentationName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.UserMessage).IsRequired();
            entity.Property(e => e.AssistantMessage).IsRequired();
            entity.Property(e => e.Timestamp).IsRequired();
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.DocumentationName);
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<Models.Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DocumentationName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CrawledAt).IsRequired();
            entity.Property(e => e.ContentLength).IsRequired();
            entity.Property(e => e.Metadata);
            entity.HasIndex(e => e.DocumentationName);
            entity.HasIndex(e => e.Url).IsUnique();
            entity.HasIndex(e => e.CrawledAt);
        });
    }

    /// <summary>
    /// Gets the most recent conversations for a specific documentation
    /// </summary>
    public async Task<List<ConversationMessage>> GetRecentConversationsByDocumentationAsync(
        string documentationName,
        int count = 20)
    {
        return await Conversations
            .Where(c => c.DocumentationName == documentationName)
            .OrderByDescending(c => c.Timestamp)
            .Take(count)
            .ToListAsync();
    }
}