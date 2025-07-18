using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using FreeTaskBackend.Services;
using FreeTaskBackend.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace FreeTaskBackend.Controllers;

[ApiController]
[Route("api/v1/chats")]
public class ChatsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ChatService _chatService;
    private readonly ILogger<ChatsController> _logger;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatsController(
        AppDbContext context,
        ChatService chatService,
        ILogger<ChatsController> logger,
        IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _chatService = chatService;
        _logger = logger;
        _hubContext = hubContext;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateChat([FromBody] CreateChatDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var chat = await _chatService.CreateChatAsync(userId, dto);
            return Ok(new ChatResponseDto
            {
                Id = chat.Id,
                OrderId = chat.OrderId,
                IsGroup = chat.IsGroup,
                CreatedAt = chat.CreatedAt
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating chat for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при создании чата" });
        }
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetChat(Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var hasAccess = await _chatService.HasAccessToChatAsync(id, userId);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} has no access to chat {ChatId}", userId, id);
                return Forbid();
            }

            var chat = await _context.Chats
                .Where(c => c.Id == id)
                .Select(c => new ChatResponseDto
                {
                    Id = c.Id,
                    OrderId = c.OrderId,
                    IsGroup = c.IsGroup,
                    CreatedAt = c.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (chat == null)
            {
                _logger.LogWarning("Chat {ChatId} not found", id);
                return NotFound(new { message = "Чат не найден" });
            }

            return Ok(chat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat {ChatId} for user {UserId}", id, userId);
            return BadRequest(new { message = "Ошибка при получении чата" });
        }
    }
    [Authorize]
    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var messages = await _chatService.GetMessagesAsync(id, userId, page, pageSize);
            var response = messages.Select(m => new MessageResponseDto
            {
                Id = m.Id,
                ChatId = m.ChatId,
                SenderId = m.SenderId,
                Content = m.Content,
                AttachmentUrl = m.AttachmentUrl,
                IsVoice = m.IsVoice,
                SentAt = m.SentAt,
                IsEdited = m.IsEdited
            }).ToList();
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages for chat: {ChatId}", id);
            return BadRequest(new { message = "Ошибка при получении сообщений" });
        }
    }

    [Authorize]
    [HttpPost("{id}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, [FromForm] SendMessageDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            _logger.LogInformation("Received request to send message to chatId: {ChatId}, isVoice: {IsVoice}", id, dto.IsVoice);
            var userId = Guid.Parse(User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                ?? User.FindFirst("sub")?.Value
                ?? throw new UnauthorizedAccessException("Invalid user ID."));

            if (!await _chatService.HasAccessToChatAsync(id, userId))
                return Forbid();

            var message = await _chatService.SendMessageAsync(id, userId, dto);

            var response = new MessageResponseDto
            {
                Id = message.Id,
                ChatId = message.ChatId,
                SenderId = message.SenderId,
                Content = message.Content,
                AttachmentUrl = message.AttachmentUrl,
                IsVoice = message.IsVoice,
                SentAt = message.SentAt,
                IsEdited = message.IsEdited
            };

            await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", response);
            _logger.LogInformation("Message sent successfully to chatId: {ChatId}, messageId: {MessageId}", id, message.Id);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Chat not found: {ChatId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to chat: {ChatId}", id);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation while sending message to chatId: {ChatId}", id);
            return BadRequest(new { message = ex.Message, details = ex.InnerException?.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to chatId: {ChatId}", id);
            return StatusCode(500, new { message = "An error occurred while sending the message.", details = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{chatId}/messages/{messageId}")]
    public async Task<IActionResult> UpdateMessage(Guid chatId, Guid messageId, [FromBody] UpdateMessageDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = Guid.Parse(User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                ?? User.FindFirst("sub")?.Value
                ?? throw new UnauthorizedAccessException("Invalid user ID."));

            if (!await _chatService.HasAccessToChatAsync(chatId, userId))
                return Forbid();

            var message = await _chatService.UpdateMessageAsync(chatId, messageId, userId, dto);

            var response = new MessageResponseDto
            {
                Id = message.Id,
                ChatId = message.ChatId,
                SenderId = message.SenderId,
                Content = message.Content,
                AttachmentUrl = message.AttachmentUrl,
                IsVoice = message.IsVoice,
                SentAt = message.SentAt,
                IsEdited = message.IsEdited
            };

            await _hubContext.Clients.Group(chatId.ToString()).SendAsync("MessageUpdated", response);

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating message {MessageId} in chat {ChatId}", messageId, chatId);
            return BadRequest(new { message = "Ошибка при редактировании сообщения" });
        }
    }

    [Authorize]
    [HttpDelete("{chatId}/messages/{messageId}")]
    public async Task<IActionResult> DeleteMessage(Guid chatId, Guid messageId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                ?? User.FindFirst("sub")?.Value
                ?? throw new UnauthorizedAccessException("Invalid user ID."));

            if (!await _chatService.HasAccessToChatAsync(chatId, userId))
                return Forbid();

            await _chatService.DeleteMessageAsync(chatId, messageId, userId);

            await _hubContext.Clients.Group(chatId.ToString()).SendAsync("MessageDeleted", messageId);

            return Ok(new { message = "Сообщение удалено" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {MessageId} in chat {ChatId}", messageId, chatId);
            return BadRequest(new { message = "Ошибка при удалении сообщения" });
        }
    }

    [Authorize]
    [HttpPost("{id}/typing")]
    public async Task<IActionResult> NotifyTyping(Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            await _chatService.NotifyTypingAsync(id, userId);
            await _hubContext.Clients.Group(id.ToString()).SendAsync("UserTyping", userId);
            return Ok(new { message = "Уведомление о наборе текста отправлено" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying typing in chat: {ChatId}", id);
            return BadRequest(new { message = "Ошибка при уведомлении о наборе текста" });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteChat(Guid id)
    {
        try
        {
            await _chatService.DeleteChatAsync(id);
            return Ok(new { message = "Чат удален" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting chat: {ChatId}", id);
            return BadRequest(new { message = "Ошибка при удалении чата" });
        }
    }

    [Authorize]
    [HttpPost("{id}/quick-replies")]
    public async Task<IActionResult> AddQuickReply(Guid id, [FromBody] AddQuickReplyDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            await _chatService.AddQuickReplyAsync(id, userId, dto);
            return Ok(new { message = "Быстрый ответ добавлен" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding quick reply for chat: {ChatId}", id);
            return BadRequest(new { message = "Ошибка при добавлении быстрого ответа" });
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetChats([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var chats = await _chatService.GetChatsAsync(userId, page, pageSize);
            return Ok(chats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chats for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при получении списка чатов" });
        }
    }
}