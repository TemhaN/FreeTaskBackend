using FreeTaskBackend.Services;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace FreeTaskBackend.Hubs;

public class ChatHub : Hub
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ChatService chatService, ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            _logger.LogInformation("Client connecting: ConnectionId={ConnectionId}", Context.ConnectionId);
            var userId = GetUserId();
            _logger.LogInformation("Client connected: ConnectionId={ConnectionId}, UserId={UserId}", Context.ConnectionId, userId);

            var chats = await _chatService.GetChatsAsync(userId, 1, 1000);
            foreach (var chat in chats)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, chat.Id.ToString());
            }

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync for ConnectionId: {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }

    public async Task JoinChat(string chatId)
    {
        try
        {
            _logger.LogInformation("Attempting to join chat: ChatId={ChatId}, ConnectionId={ConnectionId}", chatId, Context.ConnectionId);
            if (!Guid.TryParse(chatId, out var parsedChatId))
            {
                _logger.LogError("Invalid chatId format: {ChatId}", chatId);
                throw new HubException("Invalid chat ID format.");
            }

            var userId = GetUserId();
            _logger.LogInformation("User {UserId} attempting to join chat {ChatId}", userId, parsedChatId);
            var hasAccess = await _chatService.HasAccessToChatAsync(parsedChatId, userId);
            if (!hasAccess)
            {
                _logger.LogWarning("Access denied for user {UserId} to chat {ChatId}", userId, parsedChatId);
                throw new HubException("Access denied.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
            _logger.LogInformation("User {UserId} successfully joined chat {ChatId}", userId, parsedChatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining chat {ChatId} for ConnectionId: {ConnectionId}", chatId, Context.ConnectionId);
            throw;
        }
    }

    public async Task NotifyTyping(string chatId)
    {
        try
        {
            _logger.LogInformation("NotifyTyping called: ChatId={ChatId}, ConnectionId={ConnectionId}", chatId, Context.ConnectionId);
            if (!Guid.TryParse(chatId, out var parsedChatId))
            {
                _logger.LogError("Invalid chatId format: {ChatId}", chatId);
                throw new HubException("Invalid chat ID format.");
            }

            var userId = GetUserId();
            var hasAccess = await _chatService.HasAccessToChatAsync(parsedChatId, userId);
            if (!hasAccess)
            {
                _logger.LogWarning("Access denied for user {UserId} to chat {ChatId}", userId, parsedChatId);
                throw new HubException("Access denied.");
            }

            await Clients.Group(chatId).SendAsync("UserTyping", userId.ToString());
            _logger.LogInformation("User {UserId} notified typing in chat {ChatId}", userId, parsedChatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying typing in chat {ChatId} for ConnectionId: {ConnectionId}", chatId, Context.ConnectionId);
            throw;
        }
    }

    public async Task MarkChatAsRead(Guid chatId)
    {
        try
        {
            var userId = GetUserId();
            await _chatService.UpdateLastReadAtAsync(chatId, userId);
            var chats = await _chatService.GetChatsAsync(userId, 1, 1000);
            await Clients.Caller.SendAsync("UpdateChats", chats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking chat as read: ChatId={ChatId}", chatId);
            throw;
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            var claims = Context.User?.Claims.Select(c => $"{c.Type}:{c.Value}") ?? new List<string>();
            _logger.LogError("No user ID claim found in token: Claims={Claims}", string.Join(", ", claims));
            throw new HubException("Invalid user ID.");
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogError("Invalid user ID format: {UserIdClaim}", userIdClaim);
            throw new HubException("Invalid user ID.");
        }

        return userId;
    }
}