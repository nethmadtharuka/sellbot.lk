using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SellBotLk.Api.Data;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;
using SellBotLk.Api.Services;

namespace SellBotLk.Tests;

[CollectionDefinition("app")]
public class AppCollection : ICollectionFixture<TestApp> { }

/// <summary>
/// Hosts the real API against real MySQL, because row locks, conditional updates
/// and unique indexes are exactly what these tests have to prove. Only outbound
/// calls to Meta and Gemini are faked.
///
/// Needs SELLBOT_TEST_MYSQL, a connection string without a database, for example:
///   server=127.0.0.1;port=33061;user=root
/// Each run creates its own sellbot_test_* database and drops it afterwards.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AppSecret = "test-app-secret";
    public const string OwnerPhone = "94770000001";
    public const string AdminPassword = "test-admin-password-123";

    public FakeUpstream Upstream { get; } = new();
    public string AdminToken { get; private set; } = "";

    public TestApp()
    {
        var server = Environment.GetEnvironmentVariable("SELLBOT_TEST_MYSQL")
            ?? throw new InvalidOperationException(
                "Set SELLBOT_TEST_MYSQL to a MySQL connection string without a database, " +
                "e.g. server=127.0.0.1;port=33061;user=root");

        // Environment variables beat appsettings, and "Testing" skips the developer's
        // appsettings.Development.json, so a real database can't be reached from here.
        var database = $"sellbot_test_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $"{server};database={database}");
        Environment.SetEnvironmentVariable("Jwt__Secret", "test-jwt-secret-test-jwt-secret-test-jwt-secret");
        Environment.SetEnvironmentVariable("Admin__Username", "owner");
        Environment.SetEnvironmentVariable("Admin__Password", AdminPassword);
        Environment.SetEnvironmentVariable("WhatsApp__AppSecret", AppSecret);
        Environment.SetEnvironmentVariable("WhatsApp__Token", "test-token");
        Environment.SetEnvironmentVariable("WhatsApp__PhoneNumberId", "100");
        Environment.SetEnvironmentVariable("Owner__Phone", OwnerPhone);
        Environment.SetEnvironmentVariable("Gemini__ApiKey", "test-key");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
            services.ConfigureHttpClientDefaults(client =>
                client.ConfigurePrimaryHttpMessageHandler(() => new FakeHandler(Upstream))));
    }

    public async Task InitializeAsync()
    {
        await WithDbAsync(db => db.Database.MigrateAsync());

        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "owner", password = AdminPassword });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        AdminToken = body.GetProperty("token").GetString()!;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await WithDbAsync(db => db.Database.EnsureDeletedAsync());
        await base.DisposeAsync();
    }

    public HttpClient AdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AdminToken);
        return client;
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Runs against a fresh scope, like a separate request.</summary>
    public async Task<T> WithServiceAsync<TService, T>(Func<TService, Task<T>> action)
        where TService : notnull
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public Task<OrderResponseDto> CreateOrderAsync(int customerId, int productId, int quantity = 1, decimal? price = null) =>
        WithServiceAsync<OrderService, OrderResponseDto>(orders => orders.CreateOrderAsync(new CreateOrderDto
        {
            CustomerId = customerId,
            Items = new() { new() { ProductId = productId, Quantity = quantity, NegotiatedPrice = price } }
        }));

    public async Task<Customer> NewCustomerAsync()
    {
        var customer = new Customer
        {
            PhoneNumber = "9471" + Random.Shared.NextInt64(10_000_000, 99_999_999),
            Name = $"Customer {Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };
        await WithDbAsync(db => { db.Customers.Add(customer); return db.SaveChangesAsync(); });
        return customer;
    }

    public async Task<Product> NewProductAsync(
        decimal price = 1000, decimal minPrice = 800, int stock = 10,
        decimal bulkPercent = 0, int bulkMinQty = 1000)
    {
        var product = new Product
        {
            Name = $"Test Product {Guid.NewGuid():N}",
            Category = "Test",
            Price = price,
            MinPrice = minPrice,
            StockQty = stock,
            BulkDiscountPercent = bulkPercent,
            BulkMinQty = bulkMinQty,
            LowStockThreshold = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await WithDbAsync(db => { db.Products.Add(product); return db.SaveChangesAsync(); });
        return product;
    }

    public Task<int> StockOfAsync(int productId) =>
        WithDbAsync(db => db.Products.AsNoTracking()
            .Where(p => p.Id == productId).Select(p => p.StockQty).FirstAsync());

    public Task<Order> OrderAsync(int orderId) =>
        WithDbAsync(db => db.Orders.AsNoTracking()
            .Include(o => o.Items).FirstAsync(o => o.Id == orderId));

    public Task<List<Order>> OrdersForAsync(int customerId) =>
        WithDbAsync(db => db.Orders.AsNoTracking()
            .Include(o => o.Items).Where(o => o.CustomerId == customerId).ToListAsync());

    // ── Webhook helpers ────────────────────────────────────────────────────

    public static HttpRequestMessage SignedWebhook(string json, string secret = AppSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhook/whatsapp")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");
        return request;
    }

    public static string Payload(string from, string messageId, string type, object content) =>
        JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    id = "1",
                    changes = new[]
                    {
                        new
                        {
                            field = "messages",
                            value = new
                            {
                                messaging_product = "whatsapp",
                                metadata = new { display_phone_number = "1", phone_number_id = "100" },
                                contacts = new[] { new { wa_id = from, profile = new { name = "Tester" } } },
                                messages = new[]
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["from"] = from,
                                        ["id"] = messageId,
                                        ["timestamp"] = "1776235987",
                                        ["type"] = type,
                                        [type] = content
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });

    public async Task<HttpResponseMessage> PostAsync(string from, string messageId, string type, object content)
    {
        var response = await CreateClient().SendAsync(
            SignedWebhook(Payload(from, messageId, type, content)));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return response;
    }

    /// <summary>Sends a text with a unique body, so Gemini's parse cache can't answer for it.</summary>
    public Task SendTextAsync(string from, string? messageId = null) =>
        PostAsync(from, messageId ?? NewMessageId(), "text", new { body = $"msg {Guid.NewGuid():N}" });

    /// <summary>Registers an uploadable file and sends it as a WhatsApp image or document.</summary>
    public Task SendMediaAsync(string from, string type, byte[] bytes, string mime = "image/jpeg")
    {
        var mediaId = $"media{Guid.NewGuid():N}";
        Upstream.Media[mediaId] = (bytes, mime);
        return PostAsync(from, NewMessageId(), type, new { id = mediaId });
    }

    public static string NewMessageId() => $"wamid.{Guid.NewGuid():N}";
}

/// <summary>What Meta and Gemini would have done, and everything sent to them.</summary>
public class FakeUpstream
{
    public Func<string, string> Gemini { get; set; } = _ => "{}";
    public ConcurrentDictionary<string, (byte[] Bytes, string Mime)> Media { get; } = new();
    public ConcurrentQueue<JsonElement> Sent { get; } = new();
    public int GeminiCalls;
    public int MediaDownloads;

    public void Reset()
    {
        Gemini = _ => "{}";
        Sent.Clear();
        GeminiCalls = 0;
        MediaDownloads = 0;
    }

    public List<string> TextsTo(string phone) => Sent
        .Where(m => m.TryGetProperty("to", out var to) && to.GetString() == phone)
        .Select(m =>
            m.TryGetProperty("text", out var text) ? text.GetProperty("body").GetString()! :
            m.TryGetProperty("interactive", out var interactive) ? interactive.GetProperty("body").GetProperty("text").GetString()! :
            "")
        .Where(t => t.Length > 0)
        .ToList();

    public int DocumentsTo(string phone) => Sent.Count(m =>
        m.TryGetProperty("to", out var to) && to.GetString() == phone &&
        m.TryGetProperty("type", out var type) && type.GetString() == "document");
}

internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly FakeUpstream _upstream;

    public FakeHandler(FakeUpstream upstream) => _upstream = upstream;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var host = request.RequestUri!.Host;
        var path = request.RequestUri.AbsolutePath;

        if (host.Contains("generativelanguage"))
        {
            Interlocked.Increment(ref _upstream.GeminiCalls);
            var prompt = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Json(new
            {
                candidates = new[]
                {
                    new
                    {
                        finishReason = "STOP",
                        content = new { parts = new[] { new { text = _upstream.Gemini(prompt) } } }
                    }
                }
            });
        }

        if (host == "media.test")
        {
            Interlocked.Increment(ref _upstream.MediaDownloads);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_upstream.Media[path.Trim('/')].Bytes)
            };
        }

        if (path.EndsWith("/messages"))
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            _upstream.Sent.Enqueue(JsonSerializer.Deserialize<JsonElement>(body));
            return Json(new { messages = new[] { new { id = "wamid.out" } } });
        }

        if (path.EndsWith("/media"))
            return Json(new { id = "uploaded-media" });

        // Media lookup: GET /v22.0/{mediaId}
        var mediaId = path.Split('/').Last();
        return Json(new
        {
            url = $"https://media.test/{mediaId}",
            mime_type = _upstream.Media[mediaId].Mime
        });
    }

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
    };
}
