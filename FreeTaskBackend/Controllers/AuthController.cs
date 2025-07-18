using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using FreeTaskBackend.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FreeTaskBackend.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuthService _authService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, AuthService authService, EmailService emailService, IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<AuthController> logger)
    {
        _context = context;
        _authService = authService;
        _emailService = emailService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    [HttpGet("google")]
    public IActionResult GoogleLogin([FromQuery] string role = null, [FromQuery] string redirectUri = null)
    {
        if (!string.IsNullOrEmpty(role) && role != "Freelancer" && role != "Client")
        {
            _logger.LogWarning("Invalid role parameter: {Role}", role);
            return BadRequest(new { message = "Роль должна быть 'Freelancer' или 'Client'" });
        }

        // Включаем сессию
        HttpContext.Session.SetString("OAuthRole", role ?? "Client");
        var sessionId = HttpContext.Session.Id;

        var clientId = _configuration["Google:ClientId"];
        var defaultRedirectUri = _configuration["Google:RedirectUri"];
        var scope = "openid profile email";
        var finalRedirectUri = !string.IsNullOrEmpty(redirectUri) ? redirectUri : defaultRedirectUri;
        var googleAuthUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={clientId}" +
            $"&redirect_uri={Uri.EscapeDataString(finalRedirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(scope)}" +
            $"&access_type=offline" +
            $"&prompt=consent" +
            $"&state={Uri.EscapeDataString(sessionId)}";

        _logger.LogInformation("Redirecting to Google OAuth with sessionId: {SessionId}, Role: {Role}", sessionId, role ?? "Client");
        return Redirect(googleAuthUrl);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            _logger.LogWarning("Authorization code or state is missing");
            return BadRequest(new { message = "Код авторизации или state не предоставлен" });
        }

        var sessionId = state;
        _logger.LogInformation("Received GoogleCallback: Code={Code}, SessionId={SessionId}", code, sessionId);

        var role = HttpContext.Session.GetString("OAuthRole");
        if (string.IsNullOrEmpty(role) || (role != "Freelancer" && role != "Client"))
        {
            _logger.LogWarning("Role is missing or invalid in session: {Role}", role ?? "null");
            return BadRequest(new
            {
                message = "Роль не указана или недействительна",
                details = $"Received session role: {role ?? "null"}"
            });
        }

        HttpContext.Session.Remove("OAuthRole");

        try
        {
            _logger.LogInformation("Processing authorization code: {Code}, Role: {Role}", code, role);

            var client = _httpClientFactory.CreateClient();
            var redirectUri = _configuration["Google:RedirectUri"];
            _logger.LogInformation("Using redirect_uri: {RedirectUri}", redirectUri);

            var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", _configuration["Google:ClientId"] },
                { "client_secret", _configuration["Google:ClientSecret"] },
                { "redirect_uri", redirectUri },
                { "grant_type", "authorization_code" }
            })
            };

            _logger.LogInformation("Sending token request to Google");
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to exchange code for token. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                return BadRequest(new { message = "Ошибка при обмене кода на токен", details = errorContent });
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Received token response: {Json}", json);

            var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (string.IsNullOrEmpty(tokenResponse.IdToken))
            {
                _logger.LogError("IdToken is missing in Google response: {Json}", json);
                return BadRequest(new { message = "IdToken не получен от Google" });
            }

            _logger.LogInformation("Validating IdToken: {IdToken}", tokenResponse.IdToken);
            var payload = await GoogleJsonWebSignature.ValidateAsync(tokenResponse.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Google:ClientId"] }
            });

            _logger.LogInformation("IdToken validated. Subject: {Subject}, Email: {Email}", payload.Subject, payload.Email);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject || u.Email == payload.Email);
            if (user == null)
            {
                _logger.LogInformation("Creating new user with GoogleId: {GoogleId}, Role: {Role}", payload.Subject, role);
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = payload.Email,
                    Name = payload.Name,
                    GoogleId = payload.Subject,
                    Role = role,
                    VerificationStatus = "Basic",
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Users.Add(user);

                if (role == "Freelancer")
                {
                    var freelancerProfile = new FreelancerProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        Skills = new List<string>(),
                        LevelPoints = 0,
                        Level = FreelancerLevel.Newbie,
                        Rating = 0,
                        Bio = string.Empty,
                        AvatarUrl = string.Empty
                    };
                    _context.FreelancerProfiles.Add(freelancerProfile);
                }
                else if (role == "Client")
                {
                    var clientProfile = new ClientProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id
                    };
                    _context.ClientProfiles.Add(clientProfile);
                }
            }
            else
            {
                _logger.LogInformation("Found existing user: {UserId}, Role: {Role}", user.Id, user.Role);
                if (string.IsNullOrEmpty(user.GoogleId))
                {
                    user.GoogleId = payload.Subject;
                    user.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("User created or updated: {UserId}", user.Id);

            var jwtToken = _authService.GenerateJwtToken(user);
            _logger.LogInformation("Generated JWT for user: {UserId}", user.Id);

            // Редирект на фронтенд с токеном
            var frontendUrl = "http://localhost:3000/login";
            var redirectUrl = $"{frontendUrl}?token={Uri.EscapeDataString(jwtToken)}";
            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GoogleCallback: {Message}", ex.Message);
            // Редирект на фронтенд с ошибкой
            var frontendUrl = "http://localhost:3000/login";
            var errorMessage = Uri.EscapeDataString("Неверный Google токен или ошибка сервера");
            return Redirect($"{frontendUrl}?error={errorMessage}");
        }
    }


    [HttpPost("email/register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (existingUser != null)
        {
            // Проверяем пароль для входа
            if (string.IsNullOrEmpty(existingUser.PasswordHash))
            {
                _logger.LogWarning("User with email {Email} exists but has no password hash, likely registered via Google OAuth", dto.Email);
                return BadRequest(new { message = "Пользователь с этим email уже зарегистрирован через Google. Используйте вход через Google или сбросьте пароль." });
            }

            // Проверяем пароль
            if (!BCrypt.Net.BCrypt.Verify(dto.Password, existingUser.PasswordHash))
            {
                _logger.LogWarning("Invalid password attempt for existing user: {Email}", dto.Email);
                return Unauthorized(new { message = "Неверный пароль для существующего пользователя" });
            }

            // Если email не подтвержден, отправляем код верификации
            if (!existingUser.IsEmailVerified)
            {
                var emailSent = await _emailService.SendVerificationCodeAsync(existingUser.Email, "EmailVerification");
                if (!emailSent)
                    return BadRequest(new { message = "Превышен лимит запросов кода. Попробуйте снова завтра." });

                return Unauthorized(new { message = "Email не подтвержден. Новый код отправлен на вашу почту." });
            }

            // Генерируем токен для входа, не меняя имя и роль
            var token = _authService.GenerateJwtToken(existingUser);
            _logger.LogInformation("Existing user logged in during registration attempt: {Email}", existingUser.Email);
            return Ok(new TokenResponse { AccessToken = token });
        }

        // Если пользователь не существует, регистрируем нового
        var userName = string.IsNullOrEmpty(dto.Name) ? dto.Email.Split('@')[0] : dto.Name;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            Name = userName,
            Role = dto.Role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            VerificationStatus = "Basic",
            IsEmailVerified = false,
            VerificationAttempts = 0,
            LastAttemptReset = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        if (dto.Role == "Freelancer")
        {
            var freelancerProfile = new FreelancerProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Skills = new List<string>(),
                LevelPoints = 0,
                Level = FreelancerLevel.Newbie,
                Rating = 0,
                Bio = string.Empty,
                AvatarUrl = string.Empty
            };
            _context.FreelancerProfiles.Add(freelancerProfile);
        }
        else if (dto.Role == "Client")
        {
            var clientProfile = new ClientProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id
            };
            _context.ClientProfiles.Add(clientProfile);
        }

        await _context.SaveChangesAsync();

        var newEmailSent = await _emailService.SendVerificationCodeAsync(dto.Email, "EmailVerification");
        if (!newEmailSent)
            return BadRequest(new { message = "Превышен лимит запросов кода. Попробуйте снова завтра." });

        var newToken = _authService.GenerateJwtToken(user); // Возвращаем токен
        _logger.LogInformation("User registered: {Email}, verification code sent", dto.Email);
        return Ok(new { message = "Код подтверждения отправлен на вашу почту.", accessToken = newToken });
    }


    [HttpPost("email/login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Проверяем, является ли входной параметр email или именем
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email || u.Name == dto.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Неверный email, имя или пароль" });

        if (!user.IsEmailVerified)
        {
            var sent = await _emailService.SendVerificationCodeAsync(user.Email, "EmailVerification");
            if (!sent)
                return BadRequest(new { message = "Превышен лимит запросов кода. Попробуйте снова завтра." });

            return Unauthorized(new { message = "Email не подтвержден. Новый код отправлен на вашу почту." });
        }

        var token = _authService.GenerateJwtToken(user);
        _logger.LogInformation("User logged in: {Email}", user.Email);
        return Ok(new TokenResponse { AccessToken = token });
    }

    [Authorize]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
        if (user == null)
            return Unauthorized(new { message = "Пользователь не найден" });

        var token = _authService.GenerateJwtToken(user);
        return Ok(new TokenResponse { AccessToken = token });
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { message = "Выход выполнен успешно" });
    }
    [Authorize]
    [HttpPost("verify/extended")]
    public async Task<IActionResult> VerifyExtended([FromForm] VerifyExtendedDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            await _authService.VerifyExtendedAsync(userId, dto);
            return Ok(new { message = "Документы для расширенной верификации отправлены" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying extended for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при расширенной верификации" });
        }
    }

    [Authorize]
    [HttpPost("verify/professional")]
    public async Task<IActionResult> VerifyProfessional([FromForm] VerifyProfessionalDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            await _authService.VerifyProfessionalAsync(userId, dto);
            return Ok(new { message = "Сертификаты для профессиональной верификации отправлены" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying professional for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при профессиональной верификации" });
        }
    }
}