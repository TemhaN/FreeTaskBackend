using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Text.Json;

namespace FreeTaskBackend.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<FreelancerProfile> FreelancerProfiles { get; set; }
    public DbSet<ClientProfile> ClientProfiles { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Bid> Bids { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<TeamMember> TeamMembers { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<FileAttachment> FileAttachments { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<FreeTaskBackend.Models.Review> Reviews { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Analytic> Analytics { get; set; }
    public DbSet<VerificationCode> VerificationCodes { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<PortfolioItem> PortfolioItems { get; set; }
    public DbSet<ChatMember> ChatMembers { get; set; }
    public DbSet<FreeTaskBackend.Models.Invoice> Invoices { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Конфигурация для User
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Конфигурация для ChatMember (удаляем дублирование)
        modelBuilder.Entity<ChatMember>()
            .HasKey(cm => cm.Id);

        modelBuilder.Entity<ChatMember>()
            .HasOne(cm => cm.Chat)
            .WithMany(c => c.ChatMembers) // Явно указываем коллекцию
            .HasForeignKey(cm => cm.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Chat>()
            .HasMany(c => c.Messages)
            .WithOne(m => m.Chat)
            .HasForeignKey(m => m.ChatId);

        modelBuilder.Entity<Chat>()
            .HasMany(c => c.ChatMembers)
            .WithOne(cm => cm.Chat)
            .HasForeignKey(cm => cm.ChatId);

        modelBuilder.Entity<ChatMember>()
            .HasOne(cm => cm.User)
            .WithMany()
            .HasForeignKey(cm => cm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Конфигурация для FreelancerProfile
        modelBuilder.Entity<FreelancerProfile>()
            .HasOne(fp => fp.User)
            .WithOne()
            .HasForeignKey<FreelancerProfile>(fp => fp.UserId);

        // Конфигурация для ClientProfile
        modelBuilder.Entity<ClientProfile>()
            .HasOne(cp => cp.User)
            .WithOne()
            .HasForeignKey<ClientProfile>(cp => cp.UserId);

        // Конфигурация для Order
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Client)
            .WithMany()
            .HasForeignKey(o => o.ClientId);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Freelancer)
            .WithMany()
            .HasForeignKey(o => o.FreelancerId)
            .IsRequired(false);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Team)
            .WithMany()
            .HasForeignKey(o => o.TeamId)
            .IsRequired(false);

        // Конфигурация для Bid
        modelBuilder.Entity<Bid>()
            .HasOne(b => b.Order)
            .WithMany()
            .HasForeignKey(b => b.OrderId);

        modelBuilder.Entity<Bid>()
            .HasOne(b => b.Freelancer)
            .WithMany()
            .HasForeignKey(b => b.FreelancerId);

        // Конфигурация для Team
        modelBuilder.Entity<Team>()
            .HasOne(t => t.Leader)
            .WithMany()
            .HasForeignKey(t => t.LeaderId);

        // Конфигурация для TeamMember
        modelBuilder.Entity<TeamMember>()
            .HasKey(tm => new { tm.TeamId, tm.FreelancerId });

        modelBuilder.Entity<TeamMember>()
            .HasOne(tm => tm.Team)
            .WithMany()
            .HasForeignKey(tm => tm.TeamId);

        modelBuilder.Entity<TeamMember>()
            .HasOne(tm => tm.Freelancer)
            .WithMany()
            .HasForeignKey(tm => tm.FreelancerId);

        // Конфигурация для Chat
        modelBuilder.Entity<Chat>()
            .HasOne(c => c.Order)
            .WithMany()
            .HasForeignKey(c => c.OrderId)
            .IsRequired(false);

        // Конфигурация для Message
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Chat)
            .WithMany()
            .HasForeignKey(m => m.ChatId);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId);

        // Конфигурация для FileAttachment
        modelBuilder.Entity<FileAttachment>()
            .HasOne(fa => fa.Chat)
            .WithMany()
            .HasForeignKey(fa => fa.ChatId);

        modelBuilder.Entity<FileAttachment>()
            .HasOne(fa => fa.Message)
            .WithMany()
            .HasForeignKey(fa => fa.MessageId);

        // Конфигурация для Payment
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Order)
            .WithMany()
            .HasForeignKey(p => p.OrderId);

        // Конфигурация для Invoice
        modelBuilder.Entity<FreeTaskBackend.Models.Invoice>()
            .HasOne(i => i.Order)
            .WithMany()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Invoice)
            .WithMany()
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Конфигурация для Review
        modelBuilder.Entity<FreeTaskBackend.Models.Review>()
            .HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId);

        modelBuilder.Entity<FreeTaskBackend.Models.Review>()
            .HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerId);

        // Конфигурация для Notification
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId);

        // Конфигурация для Analytic
        modelBuilder.Entity<Analytic>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId);

        // Конфигурация для VerificationCode
        modelBuilder.Entity<VerificationCode>()
            .HasOne(vc => vc.User)
            .WithMany()
            .HasForeignKey(vc => vc.UserId);

        // Конфигурация для Favorite
        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.Client)
            .WithMany()
            .HasForeignKey(f => f.ClientId);

        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.Freelancer)
            .WithMany()
            .HasForeignKey(f => f.FreelancerId)
            .IsRequired(false);

        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.Team)
            .WithMany()
            .HasForeignKey(f => f.TeamId)
            .IsRequired(false);

        // Конфигурация для PortfolioItem
        modelBuilder.Entity<PortfolioItem>()
            .HasOne(pi => pi.FreelancerProfile)
            .WithMany(p => p.PortfolioItems)
            .HasForeignKey(pi => pi.FreelancerProfileId);

        modelBuilder.Entity<Order>()
            .HasMany(o => o.Invoices)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}