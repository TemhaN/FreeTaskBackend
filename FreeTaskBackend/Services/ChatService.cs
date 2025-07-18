using FreeTaskBackend.Data;
using FreeTaskBackend.Hubs;
using FreeTaskBackend.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services;

public class ChatService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FileService> _fileServiceLogger;
    private readonly ILogger<ChatService> _logger;
    private readonly IHubContext<ChatHub> _hubContext;
    public ChatService(
            AppDbContext context,
            ILogger<FileService> fileServiceLogger,
            ILogger<ChatService> logger,
            IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _fileServiceLogger = fileServiceLogger;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<Chat> CreateChatAsync(Guid userId, CreateChatDto dto)
    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("Пользователь не найден");

        var order = dto.OrderId.HasValue ? await _context.Orders.FindAsync(dto.OrderId) : null;
        if (dto.OrderId.HasValue && order == null)
            throw new KeyNotFoundException("Заказ не найден");

        // Проверка доступа к заказу
        if (order != null && order.ClientId != userId)
            throw new UnauthorizedAccessException("Только заказчик может создать чат для этого заказа");

        var recipient = dto.RecipientId.HasValue ? await _context.Users.FindAsync(dto.RecipientId) : null;
        var team = dto.TeamId.HasValue ? await _context.Teams.FindAsync(dto.TeamId) : null;

        if (dto.RecipientId.HasValue && recipient == null && dto.TeamId.HasValue && team == null)
            throw new KeyNotFoundException("Получатель или команда не найдены");

        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            OrderId = dto.OrderId,
            IsGroup = dto.IsGroup || dto.TeamId.HasValue,
            CreatedAt = DateTime.UtcNow
        };

        _context.Chats.Add(chat);

        // Добавляем клиента
        _context.ChatMembers.Add(new ChatMember { ChatId = chat.Id, UserId = userId });
        _logger.LogInformation("Added client {UserId} to chat {ChatId}", userId, chat.Id);

        // Добавляем получателя (фрилансера)
        if (dto.RecipientId.HasValue)
        {
            if (recipient != null)
            {
                _context.ChatMembers.Add(new ChatMember { ChatId = chat.Id, UserId = recipient.Id });
                _logger.LogInformation("Added recipient {RecipientId} to chat {ChatId}", recipient.Id, chat.Id);
            }
            else
            {
                _logger.LogWarning("Recipient {RecipientId} not found for chat {ChatId}", dto.RecipientId, chat.Id);
            }
        }
        // Добавляем участников команды
        else if (dto.TeamId.HasValue && team != null)
        {
            var teamMembers = await _context.TeamMembers.Where(m => m.TeamId == team.Id).ToListAsync();
            _context.ChatMembers.Add(new ChatMember { ChatId = chat.Id, UserId = team.LeaderId });
            _logger.LogInformation("Added team leader {LeaderId} to chat {ChatId}", team.LeaderId, chat.Id);
            foreach (var member in teamMembers)
            {
                _context.ChatMembers.Add(new ChatMember { ChatId = chat.Id, UserId = member.FreelancerId });
                _logger.LogInformation("Added team member {FreelancerId} to chat {ChatId}", member.FreelancerId, chat.Id);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Created chat {ChatId} for order {OrderId} with recipient {RecipientId}", chat.Id, dto.OrderId, dto.RecipientId);
        return chat;
    }

    public async Task<List<Message>> GetMessagesAsync(Guid chatId, Guid userId, int page, int pageSize)
    {
        var chat = await _context.Chats.FindAsync(chatId)
            ?? throw new KeyNotFoundException("Чат не найден");

        // Проверяем, является ли пользователь участником чата
        if (!await HasAccessToChatAsync(chatId, userId))
            throw new UnauthorizedAccessException("Доступ к чату запрещен");

        return await _context.Messages
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Message> UpdateMessageAsync(Guid chatId, Guid messageId, Guid userId, UpdateMessageDto dto)
    {
        var chat = await _context.Chats.FindAsync(chatId)
            ?? throw new KeyNotFoundException("Чат не найден");

        if (!await HasAccessToChatAsync(chatId, userId))
            throw new UnauthorizedAccessException("Доступ к чату запрещен");

        var message = await _context.Messages.FindAsync(messageId)
            ?? throw new KeyNotFoundException("Сообщение не найдено");

        if (message.ChatId != chatId)
            throw new KeyNotFoundException("Сообщение не принадлежит этому чату");

        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("Только отправитель может редактировать сообщение");

        message.Content = dto.Content;
        message.IsEdited = true;
        await _context.SaveChangesAsync();

        return message;
    }

    public async Task DeleteMessageAsync(Guid chatId, Guid messageId, Guid userId)
    {
        var chat = await _context.Chats.FindAsync(chatId)
            ?? throw new KeyNotFoundException("Чат не найден");

        if (!await HasAccessToChatAsync(chatId, userId))
            throw new UnauthorizedAccessException("Доступ к чату запрещен");

        var message = await _context.Messages.FindAsync(messageId)
            ?? throw new KeyNotFoundException("Сообщение не найдено");

        if (message.ChatId != chatId)
            throw new KeyNotFoundException("Сообщение не принадлежит этому чату");

        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("Только отправитель может удалить сообщение");

        _context.Messages.Remove(message);
        await _context.SaveChangesAsync();
    }

    public async Task NotifyTypingAsync(Guid chatId, Guid userId)
    {
        var chat = await _context.Chats.FindAsync(chatId)
            ?? throw new KeyNotFoundException("Чат не найден");

        if (!await HasAccessToChatAsync(chatId, userId))
            throw new UnauthorizedAccessException("Доступ к чату запрещен");
    }

    public async Task DeleteChatAsync(Guid chatId)
    {
        var chat = await _context.Chats.FindAsync(chatId)
            ?? throw new KeyNotFoundException("Чат не найден");

        _context.Chats.Remove(chat);
        await _context.SaveChangesAsync();
    }

    public async Task AddQuickReplyAsync(Guid chatId, Guid userId, AddQuickReplyDto dto)
    {
        var chat = await _context.Chats.FindAsync(chatId)
            ?? throw new KeyNotFoundException("Чат не найден");

        if (!await HasAccessToChatAsync(chatId, userId))
            throw new UnauthorizedAccessException("Доступ к чату запрещен");

        var user = await _context.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("Пользователь не найден");

        var profile = await _context.FreelancerProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile != null)
        {
            var quickReplies = profile.QuickReplies?.ToList() ?? new List<string>();
            quickReplies.Add(dto.Content);
            profile.QuickReplies = quickReplies;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasAccessToChatAsync(Guid chatId, Guid userId)
    {
        var chat = await _context.Chats
            .Include(c => c.Order)
            .FirstOrDefaultAsync(c => c.Id == chatId);

        if (chat == null)
        {
            _logger.LogWarning("Chat {ChatId} not found", chatId);
            return false;
        }

        var isMember = await _context.ChatMembers
            .AnyAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

        if (isMember)
        {
            _logger.LogInformation("User {UserId} is a member of chat {ChatId}", userId, chatId);
            return true;
        }

        if (chat.OrderId != null && chat.Order != null)
        {
            if (chat.Order.ClientId == userId)
            {
                _logger.LogInformation("User {UserId} is the client of order {OrderId}", userId, chat.OrderId);
                return true;
            }
            if (chat.Order.FreelancerId == userId)
            {
                _logger.LogInformation("User {UserId} is the freelancer of order {OrderId}", userId, chat.OrderId);
                return true;
            }
        }

        _logger.LogWarning("User {UserId} has no access to chat {ChatId}", userId, chatId);
        return false;
    }

    public async Task<List<ChatResponseDto>> GetChatsAsync(Guid userId, int page, int pageSize)
    {
        _logger.LogInformation("Fetching chats for user {UserId}, page {Page}, pageSize {PageSize}", userId, page, pageSize);

        var chats = await _context.ChatMembers
            .Where(cm => cm.UserId == userId)
            .Include(cm => cm.Chat)
                .ThenInclude(c => c.Order)
                    .ThenInclude(o => o!.Client)
            .Include(cm => cm.Chat)
                .ThenInclude(c => c.Order)
                    .ThenInclude(o => o!.Freelancer)
            .Include(cm => cm.Chat)
                .ThenInclude(c => c.Order)
                    .ThenInclude(o => o!.Team)
            .Include(cm => cm.Chat)
                .ThenInclude(c => c.Messages)
            .Select(cm => cm.Chat)
            .ToListAsync();

        _logger.LogInformation("Loaded {ChatCount} chats for user {UserId}", chats.Count, userId);

        var sortedChats = chats
            .OrderByDescending(c => c.Messages.Any() ? c.Messages.Max(m => m.SentAt) : c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new List<ChatResponseDto>();

        foreach (var chat in sortedChats)
        {
            string name = "Аноним";
            string avatarUrl = "";
            MessageResponseDto? lastMessage = null;
            bool hasUnreadMessages = false;

            if (chat.OrderId.HasValue && chat.Order != null)
            {
                if (chat.Order.IsAnonymous && chat.Order.ClientId != userId)
                {
                    name = "Аноним";
                    avatarUrl = "";
                }
                else
                {
                    if (chat.Order.ClientId == userId)
                    {
                        if (chat.Order.FreelancerId.HasValue && chat.Order.Freelancer != null)
                        {
                            name = chat.Order.Freelancer.Name;
                            var profile = await _context.FreelancerProfiles
                                .FirstOrDefaultAsync(p => p.UserId == chat.Order.FreelancerId);
                            avatarUrl = profile?.AvatarUrl ?? "";
                        }
                        else if (chat.Order.TeamId.HasValue && chat.Order.Team != null)
                        {
                            name = chat.Order.Team.Name;
                            avatarUrl = chat.Order.Team.AvatarUrl ?? "";
                        }
                    }
                    else
                    {
                        name = chat.Order.Client.Name;
                        var profile = await _context.ClientProfiles
                            .FirstOrDefaultAsync(p => p.UserId == chat.Order.ClientId);
                        avatarUrl = profile?.AvatarUrl ?? "";
                    }
                }
            }
            else
            {
                if (chat.IsGroup)
                {
                    Team? team = null;
                    if (chat.Order != null && chat.Order.TeamId.HasValue)
                    {
                        team = await _context.Teams
                            .FirstOrDefaultAsync(t => t.Id == chat.Order.TeamId.Value);
                    }
                    if (team != null)
                    {
                        name = team.Name;
                        avatarUrl = team.AvatarUrl ?? "";
                    }
                    else
                    {
                        name = "Групповой чат";
                    }
                }
                else
                {
                    var otherMember = await _context.ChatMembers
                        .Where(cm => cm.ChatId == chat.Id && cm.UserId != userId)
                        .Include(cm => cm.User)
                        .FirstOrDefaultAsync();
                    if (otherMember != null)
                    {
                        name = otherMember.User.Name;
                        var freelancerProfile = await _context.FreelancerProfiles
                            .FirstOrDefaultAsync(p => p.UserId == otherMember.UserId);
                        var clientProfile = await _context.ClientProfiles
                            .FirstOrDefaultAsync(p => p.UserId == otherMember.UserId);
                        avatarUrl = freelancerProfile?.AvatarUrl ?? clientProfile?.AvatarUrl ?? "";
                    }
                }
            }

            var lastMessageEntity = await _context.Messages
                .Where(m => m.ChatId == chat.Id)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();
            if (lastMessageEntity != null)
            {
                _logger.LogInformation("Found last message for chat {ChatId}: {MessageId}, SentAt: {SentAt}", chat.Id, lastMessageEntity.Id, lastMessageEntity.SentAt);
                lastMessage = new MessageResponseDto
                {
                    Id = lastMessageEntity.Id,
                    ChatId = lastMessageEntity.ChatId,
                    SenderId = lastMessageEntity.SenderId,
                    Content = lastMessageEntity.Content,
                    AttachmentUrl = lastMessageEntity.AttachmentUrl,
                    IsVoice = lastMessageEntity.IsVoice,
                    SentAt = lastMessageEntity.SentAt,
                    IsEdited = lastMessageEntity.IsEdited
                };
            }
            else
            {
                _logger.LogWarning("No messages found for chat {ChatId}", chat.Id);
            }

            var lastReadAt = await _context.ChatMembers
                .Where(cm => cm.ChatId == chat.Id && cm.UserId == userId)
                .Select(cm => cm.LastReadAt)
                .FirstOrDefaultAsync();
            hasUnreadMessages = lastMessageEntity != null && await _context.Messages
                .Where(m => m.ChatId == chat.Id && m.SentAt > (lastReadAt ?? DateTime.MinValue))
                .AnyAsync();

            result.Add(new ChatResponseDto
            {
                Id = chat.Id,
                OrderId = chat.OrderId,
                IsGroup = chat.IsGroup,
                CreatedAt = chat.CreatedAt,
                Name = name,
                AvatarUrl = avatarUrl,
                LastMessage = lastMessage,
                HasUnreadMessages = hasUnreadMessages
            });
        }

        _logger.LogInformation("Returning {ChatCount} chats for user {UserId}", result.Count, userId);
        return result;
    }
    public async Task UpdateLastReadAtAsync(Guid chatId, Guid userId)
    {
        var chatMember = await _context.ChatMembers
            .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId);
        if (chatMember != null)
        {
            chatMember.LastReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Message> SendMessageAsync(Guid chatId, Guid userId, SendMessageDto dto)
    {
        var chat = await _context.Chats.FindAsync(chatId)
            ?? throw new KeyNotFoundException("Чат не найден");

        if (!await HasAccessToChatAsync(chatId, userId))
            throw new UnauthorizedAccessException("Доступ к чату запрещен");

        string? attachmentUrl = null;
        if (dto.Attachment != null)
        {
            var fileService = new FileService(_context, _fileServiceLogger);
            attachmentUrl = await fileService.UploadFileAsync(dto.Attachment, "chats", userId);
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = userId,
            Content = dto.Content ?? string.Empty,
            AttachmentUrl = attachmentUrl,
            IsVoice = dto.IsVoice,
            SentAt = DateTime.UtcNow,
            IsEdited = false
        };
        _context.Messages.Add(message);

        // Обновляем LastReadAt для отправителя
        var chatMember = await _context.ChatMembers
            .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId);
        if (chatMember != null)
        {
            chatMember.LastReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Обновляем список чатов для всех участников
        var chatMembers = await _context.ChatMembers
            .Where(cm => cm.ChatId == chatId)
            .Select(cm => cm.UserId)
            .ToListAsync();
        foreach (var memberId in chatMembers)
        {
            var chats = await GetChatsAsync(memberId, 1, 1000);
            await _hubContext.Clients.User(memberId.ToString()).SendAsync("UpdateChats", chats);
        }

        return message;
    }
}