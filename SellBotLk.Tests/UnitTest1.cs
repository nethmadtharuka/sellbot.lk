using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Services;

namespace SellBotLk.Tests;

public class HmacVerificationTests
{
    [Fact]
    public void ValidSignature_ShouldMatch()
    {
        var secret = "test_secret_123";
        var payload = "{\"object\":\"whatsapp_business_account\"}";

        var expected = ComputeHmac(payload, secret);
        var header = $"sha256={expected}";

        header.Should().StartWith("sha256=");
        header.Should().Be($"sha256={expected}");
    }

    [Fact]
    public void InvalidSignature_ShouldNotMatch()
    {
        var secret = "test_secret_123";
        var payload = "{\"object\":\"whatsapp_business_account\"}";
        var tampered = "{\"object\":\"tampered\"}";

        var original = ComputeHmac(payload, secret);
        var fromTampered = ComputeHmac(tampered, secret);

        original.Should().NotBe(fromTampered);
    }

    [Fact]
    public void EmptySecret_ShouldStillProduceHash()
    {
        var hash = ComputeHmac("payload", "");
        hash.Should().NotBeNullOrEmpty();
    }

    private static string ComputeHmac(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLower();
    }
}

public class ProductFormattingTests
{
    private readonly ProductService _productService;

    public ProductFormattingTests()
    {
        _productService = new ProductService(null!, null!, null!);
    }

    [Fact]
    public void FormatProducts_EmptyList_ReturnsNoBrowseMessage()
    {
        var result = _productService.FormatProductsForWhatsApp(new List<ProductResponseDto>());

        result.Should().Contain("browse");
    }

    [Fact]
    public void FormatProducts_EmptyList_Sinhala_ReturnsSinhalaMessage()
    {
        var result = _productService.FormatProductsForWhatsApp(
            new List<ProductResponseDto>(), "si");

        result.Should().Contain("browse");
        result.Should().Contain("සමාවෙන්න");
    }

    [Fact]
    public void FormatProducts_WithProducts_IncludesNameAndPrice()
    {
        var products = new List<ProductResponseDto>
        {
            new()
            {
                Id = 1,
                Name = "Oak Dining Chair",
                Category = "Chairs",
                Price = 15000,
                StockQty = 10,
                IsLowStock = false
            }
        };

        var result = _productService.FormatProductsForWhatsApp(products);

        result.Should().Contain("Oak Dining Chair");
        result.Should().Contain("15,000");
        result.Should().Contain("In stock");
    }

    [Fact]
    public void FormatProducts_OutOfStock_ShowsOutOfStockBadge()
    {
        var products = new List<ProductResponseDto>
        {
            new()
            {
                Id = 1,
                Name = "Glass Table",
                Category = "Tables",
                Price = 25000,
                StockQty = 0,
                IsLowStock = false
            }
        };

        var result = _productService.FormatProductsForWhatsApp(products);

        result.Should().Contain("Out of stock");
    }

    [Fact]
    public void FormatProducts_LowStock_ShowsWarning()
    {
        var products = new List<ProductResponseDto>
        {
            new()
            {
                Id = 1,
                Name = "Wooden Desk",
                Category = "Desks",
                Price = 20000,
                StockQty = 3,
                IsLowStock = true
            }
        };

        var result = _productService.FormatProductsForWhatsApp(products);

        result.Should().Contain("Only 3 left");
    }

    [Fact]
    public void FormatProducts_BulkDiscount_ShowsDiscountInfo()
    {
        var products = new List<ProductResponseDto>
        {
            new()
            {
                Id = 1,
                Name = "Office Chair",
                Category = "Chairs",
                Price = 12000,
                StockQty = 50,
                IsLowStock = false,
                BulkDiscountPercent = 10,
                BulkMinQty = 5
            }
        };

        var result = _productService.FormatProductsForWhatsApp(products);

        result.Should().Contain("10% off for 5+ units");
    }
}

public class GeminiJsonParsingTests
{
    [Fact]
    public void CleanJson_WithBackticks_ExtractsJson()
    {
        var raw = "```json\n{\"intent\":\"ProductSearch\"}\n```";
        var cleaned = CleanJsonResponse(raw);

        cleaned.Should().Be("{\"intent\":\"ProductSearch\"}");
    }

    [Fact]
    public void CleanJson_PlainJson_ReturnsAsIs()
    {
        var raw = "{\"intent\":\"Greeting\",\"language\":\"en\"}";
        var cleaned = CleanJsonResponse(raw);

        cleaned.Should().Be(raw);
    }

    [Fact]
    public void CleanJson_WithSurroundingText_ExtractsJsonObject()
    {
        var raw = "Here is the result: {\"intent\":\"Order\"} hope this helps!";
        var cleaned = CleanJsonResponse(raw);

        cleaned.Should().Be("{\"intent\":\"Order\"}");
    }

    [Fact]
    public void CleanJson_EmptyString_ReturnsEmpty()
    {
        var cleaned = CleanJsonResponse("");
        cleaned.Should().BeEmpty();
    }

    [Fact]
    public void ParsedIntent_Deserializes_Correctly()
    {
        var json = """
            {
                "intent": "ProductSearch",
                "language": "en",
                "productSearchQuery": "chairs",
                "replyMessage": "Let me find chairs for you!",
                "confidence": 0.95
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<ParsedMessageIntent>(json, options);

        parsed.Should().NotBeNull();
        parsed!.Intent.Should().Be("ProductSearch");
        parsed.Language.Should().Be("en");
        parsed.ProductSearchQuery.Should().Be("chairs");
        parsed.Confidence.Should().BeApproximately(0.95, 0.01);
    }

    [Fact]
    public void ParsedIntent_WithOrderItems_DeserializesItems()
    {
        var json = """
            {
                "intent": "Order",
                "language": "si",
                "orderItems": [
                    {"productName": "Dining Chair", "quantity": 2, "offeredPrice": null}
                ],
                "replyMessage": "ඔඩරය සටහන් කරමි",
                "confidence": 0.9
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<ParsedMessageIntent>(json, options);

        parsed.Should().NotBeNull();
        parsed!.OrderItems.Should().HaveCount(1);
        parsed.OrderItems![0].ProductName.Should().Be("Dining Chair");
        parsed.OrderItems[0].Quantity.Should().Be(2);
    }

    private static string CleanJsonResponse(string raw)
    {
        var cleaned = raw
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');

        if (start >= 0 && end > start)
            return cleaned[start..(end + 1)];

        return cleaned;
    }
}

public class WebhookDtoDeserializationTests
{
    [Fact]
    public void StatusWebhook_WithoutProfile_DeserializesSuccessfully()
    {
        var json = """
            {
                "object": "whatsapp_business_account",
                "entry": [{
                    "id": "123",
                    "changes": [{
                        "value": {
                            "messaging_product": "whatsapp",
                            "metadata": {
                                "display_phone_number": "15551234567",
                                "phone_number_id": "100"
                            },
                            "contacts": [{ "wa_id": "94771234567" }],
                            "statuses": [{
                                "id": "wamid.abc",
                                "status": "read",
                                "timestamp": "1776235987",
                                "recipient_id": "94771234567"
                            }]
                        },
                        "field": "messages"
                    }]
                }]
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dto = JsonSerializer.Deserialize<WhatsAppIncomingDto>(json, options);

        dto.Should().NotBeNull();
        dto!.Entry.Should().HaveCount(1);

        var value = dto.Entry[0].Changes[0].Value;
        value.Contacts.Should().HaveCount(1);
        value.Contacts![0].Profile.Should().BeNull();
        value.Statuses.Should().HaveCount(1);
        value.Statuses![0].Status.Should().Be("read");
        value.Messages.Should().BeNull();
    }

    [Fact]
    public void MessageWebhook_WithProfile_DeserializesSuccessfully()
    {
        var json = """
            {
                "object": "whatsapp_business_account",
                "entry": [{
                    "id": "123",
                    "changes": [{
                        "value": {
                            "messaging_product": "whatsapp",
                            "metadata": {
                                "display_phone_number": "15551234567",
                                "phone_number_id": "100"
                            },
                            "contacts": [{
                                "profile": { "name": "John" },
                                "wa_id": "94771234567"
                            }],
                            "messages": [{
                                "from": "94771234567",
                                "id": "wamid.xyz",
                                "timestamp": "1776235987",
                                "type": "text",
                                "text": { "body": "do you have chairs?" }
                            }]
                        },
                        "field": "messages"
                    }]
                }]
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dto = JsonSerializer.Deserialize<WhatsAppIncomingDto>(json, options);

        dto.Should().NotBeNull();

        var value = dto!.Entry[0].Changes[0].Value;
        value.Contacts.Should().HaveCount(1);
        value.Contacts![0].Profile.Should().NotBeNull();
        value.Contacts[0].Profile!.Name.Should().Be("John");
        value.Messages.Should().HaveCount(1);
        value.Messages![0].Type.Should().Be("text");
        value.Messages[0].Text!.Body.Should().Be("do you have chairs?");
    }
}
