using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FreeTaskBackend.Data
{
    public class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();

            if (context.Users.Any())
            {
                return;
            }

            // Пользователи
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), Email = "freelancer1@example.com", Name = "Anna Smith", Role = "Freelancer", VerificationStatus = "Basic", IsEmailVerified = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new User { Id = Guid.NewGuid(), Email = "freelancer2@example.com", Name = "Bob Johnson", Role = "Freelancer", VerificationStatus = "Verified", IsEmailVerified = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new User { Id = Guid.NewGuid(), Email = "client1@example.com", Name = "Client One", Role = "Client", VerificationStatus = "Basic", IsEmailVerified = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new User { Id = Guid.NewGuid(), Email = "client2@example.com", Name = "Client Two", Role = "Client", VerificationStatus = "Verified", IsEmailVerified = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };
            context.Users.AddRange(users);
            context.SaveChanges();
            // Профили фрилансеров
            var freelancerProfiles = new List<FreelancerProfile>
            {
                new FreelancerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = users[0].Id,
                    Skills = new List<string> { "C#", "ASP.NET", "JavaScript" },
                    LevelPoints = 50,
                    Level = FreelancerLevel.Newbie,
                    Rating = 4.5m,
                    AvatarUrl = "http://example.com/avatar1.jpg",
                    Bio = "Web developer"
                },
                new FreelancerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = users[1].Id,
                    Skills = new List<string> { "Python", "Django", "SQL" },
                    LevelPoints = 100,
                    Level = FreelancerLevel.Specialist,
                    Rating = 4.8m,
                    AvatarUrl = "http://example.com/avatar2.jpg",
                    Bio = "Backend expert"
                }
            };

             // Элементы портфолио
             var portfolioItems = new List<PortfolioItem>
            {
                new PortfolioItem
                {
                    Id = Guid.NewGuid(),
                    FreelancerProfileId = freelancerProfiles[0].Id,
                    Url = "http://example.com/portfolio1",
                    Description = "Web app"
                },
                new PortfolioItem
                {
                    Id = Guid.NewGuid(),
                    FreelancerProfileId = freelancerProfiles[0].Id,
                    Url = "http://example.com/portfolio2",
                    Description = "API"
                },
                new PortfolioItem
                {
                    Id = Guid.NewGuid(),
                    FreelancerProfileId = freelancerProfiles[1].Id,
                    Url = "http://example.com/portfolio3",
                    Description = "Data app"
                }
            };

            // Добавление в контекст
            context.FreelancerProfiles.AddRange(freelancerProfiles);
            context.PortfolioItems.AddRange(portfolioItems);
            context.SaveChanges();

            // Профили клиентов
            var clientProfiles = new List<ClientProfile>
            {
                new ClientProfile { Id = Guid.NewGuid(), UserId = users[2].Id },
                new ClientProfile { Id = Guid.NewGuid(), UserId = users[3].Id }
            };
            context.ClientProfiles.AddRange(clientProfiles);
            context.SaveChanges();

            // Заказы
            var orders = new List<Order>
            {
                new Order { Id = Guid.NewGuid(), ClientId = users[2].Id, Title = "Web application", Description = "ASP.NET Core app", Budget = 1000, Type = "Fixed", IsTurbo = false, IsAnonymous = false, Deadline = DateTime.UtcNow.AddDays(30), Status = "Open", CreatedAt = DateTime.UtcNow },
                new Order { Id = Guid.NewGuid(), ClientId = users[3].Id, Title = "Mobile app", Description = "React Native app", Budget = 1500, Type = "Hourly", IsTurbo = true, IsAnonymous = true, Deadline = DateTime.UtcNow.AddDays(45), Status = "InProgress", CreatedAt = DateTime.UtcNow },
                new Order { Id = Guid.NewGuid(), ClientId = users[2].Id, Title = "Database setup", Description = "PostgreSQL optimization", Budget = 500, Type = "Fixed", IsTurbo = false, IsAnonymous = false, Deadline = DateTime.UtcNow.AddDays(15), Status = "Open", CreatedAt = DateTime.UtcNow }
            };
            context.Orders.AddRange(orders);
            context.SaveChanges();

            // Заявки
            var bids = new List<Bid>
            {
                new Bid { Id = Guid.NewGuid(), OrderId = orders[0].Id, FreelancerId = users[0].Id, Amount = 900, Comment = "I can do this", CreatedAt = DateTime.UtcNow },
                new Bid { Id = Guid.NewGuid(), OrderId = orders[0].Id, FreelancerId = users[1].Id, Amount = 950, Comment = "Experienced in ASP.NET", CreatedAt = DateTime.UtcNow },
                new Bid { Id = Guid.NewGuid(), OrderId = orders[1].Id, FreelancerId = users[0].Id, Amount = 1400, Comment = "Mobile app expert", CreatedAt = DateTime.UtcNow }
            };
            context.Bids.AddRange(bids);
            context.SaveChanges();
            // Команды
            var teams = new List<Team>
            {
                new Team
                {
                    Id = Guid.NewGuid(),
                    LeaderId = users[0].Id,
                    Name = "Web Dev Team",
                    Skills = new List<string> { "Web Development", "JavaScript", "CSS" },
                    Portfolio = JsonDocument.Parse(JsonSerializer.Serialize(new List<PortfolioItemDto>
                    {
                        new PortfolioItemDto { Title = "Web Project", FileUrl = "http://example.com/portfolio/web1", Description = "Sample web project" }
                    })),
                    Rating = 0,
                    AvatarUrl = ""
                },
                new Team
                {
                    Id = Guid.NewGuid(),
                    LeaderId = users[1].Id,
                    Name = "Backend Crew",
                    Skills = new List<string> { "Python", "Django", "SQL" },
                    Portfolio = JsonDocument.Parse(JsonSerializer.Serialize(new List<PortfolioItemDto>
                    {
                        new PortfolioItemDto { Title = "API Project", FileUrl = "http://example.com/portfolio/api1", Description = "Sample API project" }
                    })),
                    Rating = 0,
                    AvatarUrl = ""
                }
            };
            context.Teams.AddRange(teams);
            context.SaveChanges();

            // Участники команд
            var teamMembers = new List<TeamMember>
            {
                new TeamMember { TeamId = teams[0].Id, FreelancerId = users[0].Id, Role = "Leader", BudgetShare = 0 },
                new TeamMember { TeamId = teams[0].Id, FreelancerId = users[1].Id, Role = "Developer", BudgetShare = 0 },
                new TeamMember { TeamId = teams[1].Id, FreelancerId = users[1].Id, Role = "Leader", BudgetShare = 0 }
            };
            context.TeamMembers.AddRange(teamMembers);
            context.SaveChanges();

            // Чаты
            var chats = new List<Chat>
            {
                new Chat { Id = Guid.NewGuid(), OrderId = orders[0].Id, IsGroup = false, CreatedAt = DateTime.UtcNow },
                new Chat { Id = Guid.NewGuid(), OrderId = orders[1].Id, IsGroup = true, CreatedAt = DateTime.UtcNow },
                new Chat { Id = Guid.NewGuid(), OrderId = null, IsGroup = false, CreatedAt = DateTime.UtcNow } // Чат без заказа
            };
            context.Chats.AddRange(chats);
            context.SaveChanges();

            // Сообщения
            var messages = new List<Message>
            {
                new Message { Id = Guid.NewGuid(), ChatId = chats[0].Id, SenderId = users[0].Id, Content = "Interested in your order", SentAt = DateTime.UtcNow },
                new Message { Id = Guid.NewGuid(), ChatId = chats[0].Id, SenderId = users[2].Id, Content = "Great, let's discuss details", SentAt = DateTime.UtcNow.AddMinutes(5) },
                new Message { Id = Guid.NewGuid(), ChatId = chats[1].Id, SenderId = users[1].Id, Content = "Team meeting scheduled", SentAt = DateTime.UtcNow }
            };
            context.Messages.AddRange(messages);
            context.SaveChanges();

            // Вложения
            var fileAttachments = new List<FileAttachment>
            {
                new FileAttachment { Id = Guid.NewGuid(), ChatId = chats[0].Id, MessageId = messages[0].Id, OwnerId = users[0].Id, FilePath = "http://example.com/attachment1.pdf", FileType = "pdf", Size = 1024, CreatedAt = DateTime.UtcNow },
                new FileAttachment { Id = Guid.NewGuid(), ChatId = chats[1].Id, MessageId = messages[2].Id, OwnerId = users[1].Id, FilePath = "http://example.com/attachment2.zip", FileType = "zip", Size = 2048, CreatedAt = DateTime.UtcNow }
            };
            context.FileAttachments.AddRange(fileAttachments);
            context.SaveChanges();

            // Платежи
            var payments = new List<Payment>
            {
                new Payment { Id = Guid.NewGuid(), OrderId = orders[0].Id, Amount = 500, Status = "Completed", StripePaymentId = "pi_1", IsTest = false, CreatedAt = DateTime.UtcNow },
                new Payment { Id = Guid.NewGuid(), OrderId = orders[1].Id, Amount = 700, Status = "Pending", StripePaymentId = "pi_2", IsTest = false, CreatedAt = DateTime.UtcNow }
            };
            context.Payments.AddRange(payments);
            context.SaveChanges();

            // Отзывы
            var reviews = new List<Review>
            {
                new Review { Id = Guid.NewGuid(), OrderId = orders[0].Id, ReviewerId = users[2].Id, Rating = 5, Comment = "Great work!", IsAnonymous = false, CreatedAt = DateTime.UtcNow },
                new Review { Id = Guid.NewGuid(), OrderId = orders[1].Id, ReviewerId = users[3].Id, Rating = 4, Comment = "Good job", IsAnonymous = false, CreatedAt = DateTime.UtcNow }
            };
            context.Reviews.AddRange(reviews);
            context.SaveChanges();

            // Уведомления
            var notifications = new List<Notification>
            {
                new Notification { Id = Guid.NewGuid(), UserId = users[0].Id, Type = "NewBid", Content = "New bid on your order", IsRead = false, CreatedAt = DateTime.UtcNow },
                new Notification { Id = Guid.NewGuid(), UserId = users[2].Id, Type = "NewMessage", Content = "New message in chat", IsRead = true, CreatedAt = DateTime.UtcNow }
            };
            context.Notifications.AddRange(notifications);
            context.SaveChanges();

            // Аналитика
            var analytics = new List<Analytic>
            {
                new Analytic { Id = Guid.NewGuid(), UserId = users[0].Id, Action = "OrderViews", TargetId = orders[0].Id, CreatedAt = DateTime.UtcNow },
                new Analytic { Id = Guid.NewGuid(), UserId = users[1].Id, Action = "ProfileViews", TargetId = users[1].Id, CreatedAt = DateTime.UtcNow }
            };
            context.Analytics.AddRange(analytics);
            context.SaveChanges();

            // Коды верификации
            var verificationCodes = new List<VerificationCode>
            {
                new VerificationCode { Id = Guid.NewGuid(), UserId = users[0].Id, Code = "123456", Type = "Email", ExpiresAt = DateTime.UtcNow.AddHours(1), CreatedAt = DateTime.UtcNow },
                new VerificationCode { Id = Guid.NewGuid(), UserId = users[1].Id, Code = "654321", Type = "Email", ExpiresAt = DateTime.UtcNow.AddHours(1), CreatedAt = DateTime.UtcNow }
            };
            context.VerificationCodes.AddRange(verificationCodes);
            context.SaveChanges();

            // Отчеты
            var reports = new List<Report>
            {
                new Report { Id = Guid.NewGuid(), ReporterId = users[2].Id, TargetId = users[0].Id, TargetType = "User", Reason = "Inappropriate behavior", IsResolved = false, CreatedAt = DateTime.UtcNow },
                new Report { Id = Guid.NewGuid(), ReporterId = users[3].Id, TargetId = users[1].Id, TargetType = "User", Reason = "Spam", IsResolved = true, CreatedAt = DateTime.UtcNow }
            };
            context.Reports.AddRange(reports);
            context.SaveChanges();
        }
    }
}