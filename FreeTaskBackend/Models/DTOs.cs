using FreeTaskBackend.Attributes;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeTaskBackend.Models;

public class RegisterDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    [MinLength(8)]
    public string Password { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    [RegularExpression("^(Freelancer|Client)$", ErrorMessage = "Role must be 'Freelancer' or 'Client'")]
    public string Role { get; set; }
}

public class LoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Required]
    [MinLength(8)]
    public string Password { get; set; }
}

public class TokenResponse
{
    public string AccessToken { get; set; }
}

public class GoogleTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }
}
public class VerifyEmailDto
{
    public string Email { get; set; }
    public string Code { get; set; }
}

public class RequestPasswordResetDto
{
    public string Email { get; set; }
}

public class ResetPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Code { get; set; }

    [Required]
    [MinLength(8, ErrorMessage = "Пароль должен содержать минимум 8 символов")]
    public string NewPassword { get; set; }
}
public class UpdateFreelancerProfileDto
{
    public List<string>? Skills { get; set; }
    public string? Bio { get; set; } 
    public IFormFile? Avatar { get; set; }
}
public class SearchFreelancersDto
{
    public string? SearchTerm { get; set; }

    public decimal? MinRating { get; set; }

    public string? Level { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    public bool UseFuzzySearch { get; set; } = true;
}

public class AddPortfolioItemDto
{
    [Required]
    public IFormFile File { get; set; }

    public string? Description { get; set; }
}

public class FreelancerProfileResponseDto
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public List<string> Skills { get; set; }
    public List<PortfolioItemResponseDto> PortfolioItems { get; set; }
    public int LevelPoints { get; set; }
    public FreelancerLevel Level { get; set; }
    public decimal Rating { get; set; }
    public string AvatarUrl { get; set; }
    public string Bio { get; set; }
    public string Role { get; set; }
}
public class CreateOrderDto
{
    [Required]
    public string Title { get; set; }
    [Required]
    public string Description { get; set; }
    [Required]
    public decimal Budget { get; set; }
    [Required]
    public string Type { get; set; }
    public bool IsTurbo { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime? Deadline { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? FreelancerId { get; set; }
}
public class OrderFilterDto
{
    public string Type { get; set; }
    public string Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PlaceBidDto
{
    [Required]
    public decimal Amount { get; set; }
    public string Comment { get; set; }
}
public class CreateTeamDto
{
    [Required(ErrorMessage = "The Name field is required.")]
    public string Name { get; set; }
    public string? SkillsString { get; set; }
    public string? PortfolioString { get; set; }
    public IFormFile? Avatar { get; set; }
}

public class TeamFilterDto
{
    public string? Skills { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class AddTeamMemberDto
{
    [Required]
    public Guid FreelancerId { get; set; }
    [Required]
    public string Role { get; set; }
    [Required]
    public decimal BudgetShare { get; set; }
}

public class PlaceTeamBidDto
{
    public Guid? OrderId { get; set; }
    [Required]
    public decimal Amount { get; set; }
    public string Comment { get; set; }
    [Required]
    [RegularExpression("^(Order|Membership)$", ErrorMessage = "Type must be 'Order' or 'Membership'")]
    public string Type { get; set; }
}
public class BidResponseDto
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid FreelancerId { get; set; }
    public string FreelancerName { get; set; }
    public decimal Amount { get; set; }
    public string Comment { get; set; }
    public string Type { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
public class AcceptTeamBidDto
{
    [Required]
    public bool Accept { get; set; } 
}
public class CreateChatDto
{
    public Guid? OrderId { get; set; }
    public Guid? RecipientId { get; set; }
    public Guid? TeamId { get; set; }
    public bool IsGroup { get; set; }
}

public class ChatResponseDto
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public bool IsGroup { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Name { get; set; } 
    public string AvatarUrl { get; set; }
    public MessageResponseDto? LastMessage { get; set; }
    public bool HasUnreadMessages { get; set; }
}

public class SendMessageDto : IValidatableObject
{
    public string? Content { get; set; }
    [FileSize(10_000_000)]
    public IFormFile? Attachment { get; set; }
    public bool IsVoice { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Content) && Attachment == null)
        {
            yield return new ValidationResult(
                "At least one of Content or Attachment must be provided.",
                new[] { nameof(Content), nameof(Attachment) }
            );
        }
    }
}
public class MessageResponseDto
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; }
    public string? AttachmentUrl { get; set; }
    public bool IsVoice { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsEdited { get; set; }
}
public class UpdateMessageDto
{
    public string Content { get; set; }
}

public class AddQuickReplyDto
{
    [Required]
    public string Content { get; set; }
}

public class CreateTestPaymentDto
{
    [Required]
    public Guid OrderId { get; set; }
    [Required]
    public decimal Amount { get; set; }
}

public class PaymentResponseDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public string StripePaymentId { get; set; }
    public bool IsTest { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ClientSecret { get; set; }
}

public class OpenDisputeDto
{
    [Required]
    public string Reason { get; set; }
}

public class CreateReviewDto
{
    [Required]
    public Guid OrderId { get; set; }
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }
    public string Comment { get; set; }
    public bool IsAnonymous { get; set; }
}

public class DisputeReviewDto
{
    [Required]
    public string Reason { get; set; }
}

public class NotificationResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; }
    public string Content { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TestNotificationDto
{
    [Required]
    public Guid UserId { get; set; }
    [Required]
    public string Content { get; set; }
}

public class UploadFileDto
{
    [Required]
    public IFormFile File { get; set; }
    [Required]
    public string Type { get; set; }
}

public class ReportContentDto
{
    [Required]
    public Guid TargetId { get; set; }
    [Required]
    public string TargetType { get; set; }
    [Required]
    public string Reason { get; set; }
}

public class ReportResponseDto
{
    public Guid Id { get; set; }
    public Guid ReporterId { get; set; }
    public Guid TargetId { get; set; }
    public string TargetType { get; set; }
    public string Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateOrderStatusDto
{
    [Required]
    public string Status { get; set; }
}

public class ExtendDeadlineDto
{
    [Required]
    public DateTime NewDeadline { get; set; }
}

public class AddMilestonesDto
{
    [Required]
    public List<MilestoneDto> Milestones { get; set; }
}

public class MilestoneDto
{
    [Required]
    public decimal Amount { get; set; }
    [Required]
    public string Description { get; set; }
    [Required]
    public DateTime Deadline { get; set; }
}

public class UpdateTeamPortfolioDto
{
    public List<PortfolioItemDto> Items { get; set; } = new List<PortfolioItemDto>();
    public IFormFile File { get; set; }
    public IFormFile? Avatar { get; set; }
    public string? Description { get; set; }
}

public class AnalyticResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; }
    public Guid? TargetId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class VerifyExtendedDto
{
    [Required]
    public IFormFile Document { get; set; }
}

public class VerifyProfessionalDto
{
    [Required]
    public List<IFormFile> Certificates { get; set; }
}
public class PortfolioItemDto
{
    public string Title { get; set; }
    public string? Description { get; set; }
    public string FileUrl { get; set; }
}
public class UpdateClientProfileDto
{
    public string CompanyName { get; set; }
    public string Bio { get; set; }
    public IFormFile Avatar { get; set; }
}

public class ClientProfileResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string CompanyName { get; set; }
    public string Bio { get; set; }
    public string AvatarUrl { get; set; }
}
public class RequestVerificationCodeDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    [RegularExpression("^(EmailVerification|PasswordReset)$", ErrorMessage = "Type must be 'EmailVerification' or 'PasswordReset'")]
    public string Type { get; set; }
}
public class GoogleCallbackRequestDto
{
    public string Code { get; set; }
    public string? State { get; set; }
}
public class AddFavoriteDto
{
    public Guid? FreelancerId { get; set; }
    public Guid? TeamId { get; set; }
}
public class FavoriteResponseDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid? FreelancerId { get; set; }
    public string? FreelancerName { get; set; }
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Action { get; set; }
}
    public class PortfolioItemResponseDto
{
    public Guid Id { get; set; }
    public string Url { get; set; }
    public string? Description { get; set; }
}
public class TeamResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Guid LeaderId { get; set; }
    public string LeaderName { get; set; }
    public List<string> Skills { get; set; }
    public List<PortfolioItemDto> Portfolio { get; set; }
    public decimal Rating { get; set; }
    public string AvatarUrl { get; set; }
}

public class UserProfileResponseDto
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public string Role { get; set; }
    public List<string>? Skills { get; set; }
    public List<PortfolioItemResponseDto>? PortfolioItems { get; set; }
    public int? LevelPoints { get; set; }
    public FreelancerLevel? Level { get; set; }
    public decimal? Rating { get; set; }
    public string CompanyName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
}
public class AcceptOrderDto
{
    public bool Accept { get; set; } // true = принять, false = отклонить
}
public class ClientDto
{
    public Guid? Id { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
}

public class OrderResponseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public decimal Budget { get; set; }
    public string Type { get; set; }
    public bool IsTurbo { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime? Deadline { get; set; }
    public string Status { get; set; }
    public Guid? ClientId { get; set; }
    public ClientDto? Client { get; set; }
    public Guid? FreelancerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<InvoiceResponseDto> Invoices { get; set; }
}

public class CreateInvoiceDto
{
    public decimal Amount { get; set; }
}

public class InvoiceResponseDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePaymentDto
{
    public Guid OrderId { get; set; }
    public Guid InvoiceId { get; set; } // Новый
    public decimal Amount { get; set; }
}

public class AnalyticsResponseDto
{
    public List<OrderResponseDto> RecentOrders { get; set; } = new List<OrderResponseDto>();
    public UserStatsDto Statistics { get; set; }
}

public class UserStatsDto
{
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal AverageRating { get; set; }
    public int ActiveOrders { get; set; }
}
public class ReviewResponseDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? ReviewerId { get; set; }
    public string ReviewerName { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; }
}