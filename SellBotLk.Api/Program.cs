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

// Services
builder.Services.AddHttpClient<WhatsAppSendService>();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddScoped<WhatsAppSendService>();
builder.Services.AddScoped<GeminiService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<MessageProcessingService>();
builder.Services.AddMemoryCache();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<VisualSearchService>();
builder.Services.AddScoped<MediaDownloadService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<HmacVerificationMiddleware>();
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();