using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SellBotLk.Api;
using SellBotLk.Api.Controllers;
using SellBotLk.Api.Data;
using SellBotLk.Api.Data.Repositories;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Middleware;
using SellBotLk.Api.Services;

LegacyEnvConfiguration.LoadLocalDotEnv();
var builder = WebApplication.CreateBuilder(args);
LegacyEnvConfiguration.ApplyFlatEnvAliases(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminDashboard", policy =>
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Database connection is not configured. For local Development, set ConnectionStrings:DefaultConnection " +
        "in appsettings.Development.json (replace YOUR_MYSQL_PASSWORD), or set DB_CONNECTION_STRING in SellBotLk.Api/.env, " +
        "or use: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"Server=...\"");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Repositories
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<OrderRepository>();

// Gemini integrations
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHttpClient<GeminiVisionService>();

// WhatsApp integrations
builder.Services.AddHttpClient<WhatsAppSendService>();
builder.Services.AddHttpClient<MediaDownloadService>();

// Services
builder.Services.AddScoped<WhatsAppSendService>();
builder.Services.AddScoped<GeminiService>();
builder.Services.AddScoped<GeminiVisionService>();
builder.Services.AddScoped<MediaDownloadService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<DeliveryService>();
builder.Services.AddScoped<PaymentMatchingService>();
builder.Services.AddScoped<VisualSearchService>();
builder.Services.AddScoped<MessageProcessingService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<NegotiationService>();
builder.Services.AddSingleton<InvoiceService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddHostedService<MaintenanceService>();

// Refuse to start with unsafe admin credentials: there are no defaults to fall back on.
if (string.IsNullOrWhiteSpace(builder.Configuration["Admin:Username"]) ||
    !AuthController.IsAcceptablePassword(builder.Configuration["Admin:Password"]))
    throw new InvalidOperationException(
        "Admin credentials are not configured. Set ADMIN_USERNAME and ADMIN_PASSWORD " +
        $"(at least {AuthController.MinPasswordLength} characters) in SellBotLk.Api/.env, " +
        "or Admin__Username and Admin__Password in the environment.");

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException(
        "Jwt:Secret must be configured with at least 32 bytes (JWT_SECRET in .env).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "SellBotLk",
            ValidAudience = "SellBotLk.Admin",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
builder.Services.AddAuthorization();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    // Overall safety valve for signed webhook traffic, status callbacks included.
    // Meta retries a 429, so hitting it delays messages rather than losing them.
    options.AddFixedWindowLimiter("webhook", limiter =>
    {
        limiter.PermitLimit = 600;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    // ponytail: partitions by client IP, so behind a reverse proxy add
    // UseForwardedHeaders or every client shares one bucket.
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Per-sender limit, applied in the webhook controller because the sender's number
// is inside the signed body. One noisy number can't use up everyone's capacity.
// ponytail: per-process counters, move to a shared store if this runs on several instances.
builder.Services.AddSingleton(PartitionedRateLimiter.Create<string, string>(sender =>
    RateLimitPartition.GetFixedWindowLimiter(sender, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 20,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    })));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SellBotLk API",
        Version = "v1",
        Description = "WhatsApp commerce platform API for SellBot.lk"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(error =>
{
    error.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            title = "An unexpected error occurred.",
            status = 500
        });
    });
});

app.UseCors("AdminDashboard");
// Signature check first, so unsigned traffic can't use up the webhook's rate limit.
app.UseMiddleware<HmacVerificationMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Lets the integration tests host this app.
public partial class Program { }