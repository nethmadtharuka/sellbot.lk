using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Data;
using SellBotLk.Api.Data.Repositories;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Middleware;
using SellBotLk.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Repositories
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<OrderRepository>();

// HTTP Clients
builder.Services.AddHttpClient<WhatsAppSendService>();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHttpClient<GeminiVisionService>();
builder.Services.AddHttpClient<MediaDownloadService>();

// Services
builder.Services.AddScoped<WhatsAppSendService>();
builder.Services.AddScoped<GeminiService>();
builder.Services.AddScoped<GeminiVisionService>();
builder.Services.AddScoped<MediaDownloadService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<VisualSearchService>();
builder.Services.AddScoped<OrderNumberGenerator>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<NegotiationService>();
builder.Services.AddScoped<MessageProcessingService>();
builder.Services.AddMemoryCache();

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

app.UseMiddleware<HmacVerificationMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();