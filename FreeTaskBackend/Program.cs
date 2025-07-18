using FreeTaskBackend.Data;
using FreeTaskBackend.Hubs;
using FreeTaskBackend.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using StackExchange.Redis;
using Stripe;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var singletonDataSource = sp.GetRequiredService<NpgsqlDataSource>();
    options.UseNpgsql(singletonDataSource, b => b.MigrationsAssembly("FreeTaskBackend"));
});

builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<CleanupService>();
builder.Services.AddScoped<FavoriteService>();
builder.Services.AddScoped<ModerationService>();
builder.Services.AddScoped<ModerationService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<FreelancerLevelService>();
builder.Services.AddScoped<FreelancerProfileService>();
builder.Services.AddScoped<FreeTaskBackend.Services.ReviewService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured"))),
        NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    };
    options.MapInboundClaims = true;
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var accessToken = context.Request.Query["access_token"].FirstOrDefault();
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            var path = context.Request.Path.Value;

            logger.LogInformation("JWT Auth check: Path={Path}, Method={Method}, AuthHeader={AuthHeader}, QueryToken={QueryToken}",
                path, context.Request.Method, authHeader ?? "None", accessToken ?? "None");

            if (path != null && path.StartsWith("/chatHub") && !string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
                logger.LogInformation("Using query access_token for SignalR: {Token}", accessToken);
            }
            else if (!string.IsNullOrEmpty(authHeader))
            {
                context.Token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authHeader.Substring("Bearer ".Length).Trim()
                    : authHeader.Trim();
                logger.LogInformation("Using Authorization header token: {Token}", context.Token);
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var sub = context.Principal?.FindFirst("sub")?.Value;
            var nameIdentifier = context.Principal?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            logger.LogInformation("Token validated: Sub={Sub}, NameIdentifier={NameIdentifier}, Path={Path}",
                sub ?? "null", nameIdentifier ?? "null", context.Request.Path.Value);

            if (!string.IsNullOrEmpty(sub))
            {
                var identity = context.Principal?.Identity as ClaimsIdentity;
                if (!context.Principal.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, sub));
                    identity?.AddClaim(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", sub));
                }
            }

            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Authentication failed: Path={Path}, Message={Message}, Exception={Exception}",
                context.Request.Path.Value, context.Exception.Message, context.Exception.StackTrace);
            return Task.CompletedTask;
        }
    };
}).AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Google:ClientId"];
    options.ClientSecret = builder.Configuration["Google:ClientSecret"];
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FreeTask API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

builder.Services.AddSignalR();
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer();

builder.Services.AddScoped<FreeTaskBackend.Services.FileService>();


StripeConfiguration.ApiKey = builder.Configuration["Stripe:TestSecretKey"];
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("AllowedCorsOrigins").Get<string[]>();
    options.AddPolicy("DynamicCorsPolicy", builder =>
    {
        builder
            .WithOrigins(allowedOrigins) 
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
builder.Services.AddAuthorization();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Jwt:Key loaded: {JwtKey}", builder.Configuration["Jwt:Key"]);
logger.LogInformation("Jwt:Issuer loaded: {JwtIssuer}", builder.Configuration["Jwt:Issuer"]);
logger.LogInformation("Jwt:Audience loaded: {JwtAudience}", builder.Configuration["Jwt:Audience"]);

app.UseCors("DynamicCorsPolicy");
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Incoming request: Path={Path}, Method={Method}, Headers={Headers}",
        context.Request.Path, context.Request.Method,
        string.Join("|", context.Request.Headers.Select(h => $"{h.Key}:{h.Value}")));
    await next(context);
});
var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
logger.LogInformation("wwwroot path: {Path}, Exists: {Exists}", wwwrootPath, Directory.Exists(wwwrootPath));
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "attachments")),
    RequestPath = "/attachments",
    OnPrepareResponse = ctx =>
    {
        var logger = ctx.Context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Static file request: Path={RequestPath}, PhysicalPath={PhysicalPath}, Exists={Exists}, Status={Status}",
            ctx.Context.Request.Path, ctx.File.PhysicalPath, ctx.File.Exists, ctx.Context.Response.StatusCode);
        if (!ctx.File.Exists)
        {
            logger.LogWarning("File not found: {PhysicalPath}", ctx.File.PhysicalPath);
            ctx.Context.Response.StatusCode = 404;
        }
    },
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("After static files: Path={Path}, Status={Status}", context.Request.Path, context.Response.StatusCode);
    await next(context);
});
app.UseSession();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FreeTask API V1");
    c.OAuthClientId("swagger-ui");
    c.OAuthAppName("Swagger UI");
});

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    DbInitializer.Initialize(context);
}


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
app.UseHangfireDashboard();

app.Run();