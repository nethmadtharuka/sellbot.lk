using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Data;
using SellBotLk.Api.Data.Repositories;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Middleware;
using SellBotLk.Api.Services;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<OrderNumberGenerator>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<DeliveryService>();
builder.Services.AddScoped<PaymentMatchingService>();
builder.Services.AddScoped<VisualSearchService>();
builder.Services.AddScoped<MessageProcessingService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<NegotiationService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AdminDashboard");
app.UseMiddleware<HmacVerificationMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();