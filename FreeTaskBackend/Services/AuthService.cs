using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services;

public class AuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly AppDbContext _context;
    private readonly ILogger<FileService> _fileServiceLogger;
    public AuthService(IConfiguration configuration, ILogger<AuthService> logger, ILogger<FileService> fileServiceLogger, AppDbContext context)
    {
        _fileServiceLogger = fileServiceLogger;
        _configuration = configuration;
        _logger = logger;
        _context = context;
    }

    public string GenerateJwtToken(User user)
    {
        _logger.LogInformation("Generating JWT for user: {UserId}", user.Id);
        var jwtKey = _configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            _logger.LogError("Jwt:Key is missing in configuration");
            throw new InvalidOperationException("Jwt:Key is not configured");
        }

        _logger.LogInformation("Using Jwt:Key: {JwtKey}", jwtKey);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        _logger.LogInformation("Claims for JWT: {Claims}", string.Join(", ", claims.Select(c => $"{c.Type}={c.Value}")));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogInformation("JWT generated successfully for user: {UserId}", user.Id);
        return tokenString;
    }

    public async Task VerifyExtendedAsync(Guid userId, VerifyExtendedDto dto)
    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("Пользователь не найден");

        var fileService = new FileService(_context, _fileServiceLogger);
        var fileUrl = await fileService.UploadFileAsync(dto.Document, "verifications", userId);

        user.VerificationStatus = "Extended";
        user.VerificationDocumentUrl = fileUrl;
        await _context.SaveChangesAsync();
    }

    public async Task VerifyProfessionalAsync(Guid userId, VerifyProfessionalDto dto)
    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("Пользователь не найден");

        var fileService = new FileService(_context, _fileServiceLogger);
        var certificateUrls = new List<string>();
        foreach (var certificate in dto.Certificates)
        {
            var fileUrl = await fileService.UploadFileAsync(certificate, "verifications", userId);
            certificateUrls.Add(fileUrl);
        }

        user.VerificationStatus = "Professional";
        user.CertificateUrls = certificateUrls;
        await _context.SaveChangesAsync();
    }
}