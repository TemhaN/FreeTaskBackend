using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeTaskBackend.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public string Role { get; set; }
    public string? GoogleId { get; set; }
    public string VerificationStatus { get; set; }
    public bool IsEmailVerified { get; set; }
    public int VerificationAttempts { get; set; }
    public DateTime? LastAttemptReset { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? PasswordHash { get; set; }
    public string? VerificationDocumentUrl { get; set; }
    public List<string>? CertificateUrls { get; set; }
}
public class VerificationCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public string Code { get; set; }
    public string Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
}
public enum FreelancerLevel
{
    Newbie,
    Specialist,
    Expert
}

public class FreelancerProfile
{
    [Key]
    public Guid Id { get; set; }

    [ForeignKey("User")]
    public Guid UserId { get; set; }

    public User User { get; set; }

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = new List<string>();

    public List<PortfolioItem> PortfolioItems { get; set; } = new List<PortfolioItem>();

    public int LevelPoints { get; set; } = 0;

    public FreelancerLevel Level { get; set; } = FreelancerLevel.Newbie;

    public decimal Rating { get; set; } = 0;

    public string AvatarUrl { get; set; } = "";

    public string Bio { get; set; }
    public List<string>? QuickReplies { get; set; } = new List<string>();
}

public class PortfolioItem
{
    public Guid Id { get; set; }

    [ForeignKey("FreelancerProfile")]
    public Guid FreelancerProfileId { get; set; }

    public FreelancerProfile FreelancerProfile { get; set; }

    public string Url { get; set; }
    public string Description { get; set; }
}
public class ClientProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public string? CompanyName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public User Client { get; set; }
    public Guid? FreelancerId { get; set; }
    public User? Freelancer { get; set; }
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public decimal Budget { get; set; }
    public string Type { get; set; }
    public bool IsTurbo { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime? Deadline { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Invoice> Invoices { get; set; } = new List<Invoice>();
}
public class Bid
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid FreelancerId { get; set; }
    public User Freelancer { get; set; }
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }
    public decimal Amount { get; set; }
    public string Comment { get; set; }
    public string Type { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Guid LeaderId { get; set; }
    public User Leader { get; set; }
    public List<string> Skills { get; set; } = new List<string>();
    public JsonDocument Portfolio { get; set; }
    public decimal Rating { get; set; }
    public string AvatarUrl { get; set; } = "";
}

public class TeamMember
{
    public Guid TeamId { get; set; }
    public Team Team { get; set; }
    public Guid FreelancerId { get; set; }
    public User Freelancer { get; set; }
    public string Role { get; set; }
    public decimal BudgetShare { get; set; }
}

public class Chat
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
    public bool IsGroup { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ChatMember> ChatMembers { get; set; } = new List<ChatMember>();
    public List<Message> Messages { get; set; } = new List<Message>();
}

public class Message
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public Chat Chat { get; set; }
    public Guid SenderId { get; set; }
    public User Sender { get; set; }
    public string Content { get; set; }
    public string? AttachmentUrl { get; set; }
    public bool IsVoice { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsEdited { get; set; }
}

public class FileAttachment
{
    public Guid Id { get; set; }
    public Guid? ChatId { get; set; }
    public Chat? Chat { get; set; }
    public Guid? MessageId { get; set; }
    public Message? Message { get; set; }
    public Guid OwnerId { get; set; } 
    public string FilePath { get; set; }
    public string FileType { get; set; }
    public string ContentType => GetContentType(FileType);
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }

    private string GetContentType(string fileType)
    {
        return fileType switch
        {
            "jpg" => "image/jpeg",
            "png" => "image/png",
            "pdf" => "application/pdf",
            "mp3" => "audio/mpeg",
            _ => "application/octet-stream"
        };
    }
}

public class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; }
    public decimal Amount { get; set; }
    public Guid? InvoiceId { get; set; } // Новый
    public Invoice? Invoice { get; set; }
    public string Status { get; set; }
    public string StripePaymentId { get; set; }
    public bool IsTest { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Review
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; }
    public Guid ReviewerId { get; set; }
    public User Reviewer { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public string Type { get; set; }
    public string Content { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
public class Favorite
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public User Client { get; set; }
    public Guid? FreelancerId { get; set; }
    public User? Freelancer { get; set; }
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }
    public DateTime CreatedAt { get; set; }
}


public class Analytic
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public string Action { get; set; }
    public Guid? TargetId { get; set; }
    public DateTime CreatedAt { get; set; }
}
public class Report
{
    public Guid Id { get; set; }
    public Guid ReporterId { get; set; }
    public Guid TargetId { get; set; }
    public string TargetType { get; set; }
    public string Reason { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChatMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChatId { get; set; }
    public Chat Chat { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public DateTime? LastReadAt { get; set; }
}

public class Invoice
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Paid
    public DateTime CreatedAt { get; set; }
}