using SellBotLk.Api.Data;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
namespace SellBotLk.Api.Services;
using SellBotLk.Api.Models;
public class MessageProcessingService
{
    private readonly GeminiService _geminiService;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly ProductService _productService;
    private readonly VisualSearchService _visualSearchService;
    private readonly OrderService _orderService;
    private readonly AppDbContext _db;
    private readonly ILogger<MessageProcessingService> _logger;
    private readonly NegotiationService _negotiationService;

    public MessageProcessingService(
        GeminiService geminiService,
        WhatsAppSendService whatsAppSendService,
        ProductService productService,
        VisualSearchService visualSearchService,
        OrderService orderService,
        AppDbContext db,
        NegotiationService negotiationService,

        ILogger<MessageProcessingService> logger)
    {
        _geminiService = geminiService;
        _whatsAppSendService = whatsAppSendService;
        _productService = productService;
        _visualSearchService = visualSearchService;
        _orderService = orderService;
        _db = db;
        _logger = logger;
        _negotiationService = negotiationService;

    }
    // 💰 NEGOTIATION HANDLER
private async Task HandleNegotiationAsync(
    string fromPhone,
    ParsedMessageIntent parsed,
    int customerId,
    string language)
{
    // Check if there's an active negotiation context first
    var existingContext = await _negotiationService.GetNegotiationContextAsync(customerId);

    // Customer replied "confirm" to accept a counteroffer
    if (existingContext != null &&
        parsed.Intent == "PriceNegotiation" &&
        (parsed.ReplyMessage?.Contains("confirm",
            StringComparison.OrdinalIgnoreCase) == true ||
         parsed.OfferedPrice == existingContext.CounterOffer))
    {
        // Place the order at the negotiated price
        var createDto = new CreateOrderDto
        {
            CustomerId = customerId,
            Items = new List<OrderItemDto>
            {
                new()
                {
                    ProductId = existingContext.ProductId,
                    Quantity = existingContext.Quantity,
                    NegotiatedPrice = existingContext.CounterOffer
                }
            }
        };

        var order = await _orderService.CreateOrderAsync(createDto);
        var confirmMsg = _orderService.FormatOrderConfirmationMessage(order);

        await _negotiationService.ClearNegotiationContextAsync(customerId);
        await _whatsAppSendService.SendTextMessageAsync(fromPhone, confirmMsg);
        return;
    }

    // Fresh negotiation — need product and offered price
    var productId = parsed.ProductId;
    var offeredPrice = parsed.OfferedPrice;
    var quantity = parsed.OrderItems?.FirstOrDefault()?.Quantity ?? 1;

    // If no product ID, try to match from order items
    if (productId == null && parsed.OrderItems?.Any() == true)
    {
        var allProducts = await _productService.GetAllAsync();
        var match = allProducts.FirstOrDefault(p =>
            p.Name.Contains(parsed.OrderItems[0].ProductName,
                StringComparison.OrdinalIgnoreCase));
        productId = match?.Id;
    }

    if (productId == null || offeredPrice == null)
    {
        await _whatsAppSendService.SendTextMessageAsync(fromPhone,
            "Which product would you like to negotiate on, " +
            "and what price are you offering? " +
            "Example: 'Can I get the leather chair for LKR 25,000?'");
        return;
    }

    var result = await _negotiationService.EvaluateOfferAsync(
        productId.Value, quantity, offeredPrice.Value, language);

    // Save context for multi-turn if counteroffer
    if (result.Outcome == NegotiationOutcome.CounterOffer)
    {
        var product = await _productService.GetByIdAsync(productId.Value);
        await _negotiationService.SaveNegotiationContextAsync(customerId,
            new NegotiationContext
            {
                ProductId = productId.Value,
                ProductName = product?.Name ?? "",
                Quantity = quantity,
                OriginalPrice = product?.Price ?? 0,
                MinPrice = product?.MinPrice ?? 0,
                CustomerLastOffer = offeredPrice,
                CounterOffer = result.CounterOfferPrice,
                RoundsRemaining = 2
            });
    }
    else if (result.Outcome == NegotiationOutcome.Accepted)
    {
        // Create order immediately at accepted price
        var createDto = new CreateOrderDto
        {
            CustomerId = customerId,
            Items = new List<OrderItemDto>
            {
                new()
                {
                    ProductId = productId.Value,
                    Quantity = quantity,
                    NegotiatedPrice = result.AcceptedPrice
                }
            }
        };

        await _orderService.CreateOrderAsync(createDto);
        await _negotiationService.ClearNegotiationContextAsync(customerId);
    }

    await _whatsAppSendService.SendTextMessageAsync(fromPhone, result.Message);
}



    // ✉️ HANDLE TEXT MESSAGES
    public async Task ProcessTextMessageAsync(
        string fromPhone, string messageText, string senderName)
    {
        var customer = await GetOrCreateCustomerAsync(fromPhone, senderName);
        var conversation = await GetOrCreateConversationAsync(customer.Id);
        var context = conversation.Context ?? "";

        _logger.LogInformation(
            "Processing message from {Phone} — State: {State}",
            MaskPhone(fromPhone), conversation.State);

        var parsed = await _geminiService.ParseMessageAsync(
            messageText, customer.Name ?? senderName, context);

        _logger.LogInformation(
            "Intent: {Intent} ({Confidence:P0}) — Language: {Lang}",
            parsed.Intent, parsed.Confidence, parsed.Language);

        if (customer.PreferredLanguage != parsed.Language)
            customer.PreferredLanguage = parsed.Language;

        conversation.State = parsed.Intent switch
        {
            "Greeting"         => ConversationState.Greeting,
            "ProductSearch"    => ConversationState.Browsing,
            "Order"            => ConversationState.Ordering,
            "PriceNegotiation" => ConversationState.Negotiating,
            "Complaint" or "OrderStatus" or "DeliveryInfo"
                               => ConversationState.Support,
            _                  => conversation.State
        };

        conversation.LastMessageAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        switch (parsed.Intent)
        {
            case "ProductSearch":
                await HandleProductSearchAsync(
                    fromPhone,
                    parsed.ProductSearchQuery ?? messageText,
                    parsed.Language);
                break;

            case "Order":
                await HandleOrderAsync(
                    fromPhone, parsed, customer.Id, parsed.Language);
                break;

                case "PriceNegotiation":
                await HandleNegotiationAsync(
        fromPhone, parsed, customer.Id, parsed.Language);
    break;

            default:
                await _whatsAppSendService.SendTextMessageAsync(
                    fromPhone, parsed.ReplyMessage);
                break;
        }
    }

    // 🖼️ HANDLE IMAGE MESSAGES
    public async Task ProcessImageMessageAsync(
        string fromPhone, byte[] imageBytes, string mimeType, string senderName)
    {
        var customer = await GetOrCreateCustomerAsync(fromPhone, senderName);
        var conversation = await GetOrCreateConversationAsync(customer.Id);

        _logger.LogInformation(
            "Processing image from {Phone} — running visual search",
            MaskPhone(fromPhone));

        await _whatsAppSendService.SendTextMessageAsync(fromPhone,
            "🔍 Analyzing your image, please wait...");

        var result = await _visualSearchService.SearchByImageAsync(imageBytes, mimeType);
        var message = _visualSearchService.FormatVisualSearchResultForWhatsApp(result);

        conversation.State = ConversationState.Browsing;
        conversation.LastMessageAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _whatsAppSendService.SendTextMessageAsync(fromPhone, message);
    }

    // 🛒 ORDER HANDLER
    private async Task HandleOrderAsync(
        string fromPhone,
        ParsedMessageIntent parsed,
        int customerId,
        string language)
    {
        if (parsed.OrderItems == null || !parsed.OrderItems.Any())
        {
            await _whatsAppSendService.SendTextMessageAsync(fromPhone,
                "I'd love to help you order! Could you tell me which product " +
                "and quantity you'd like? For example: 'I want 2 dining chairs'");
            return;
        }

        var allProducts = await _productService.GetAllAsync();
        var orderItems = new List<OrderItemDto>();

        foreach (var item in parsed.OrderItems)
        {
            var match = allProducts.FirstOrDefault(p =>
                p.Name.Contains(item.ProductName,
                    StringComparison.OrdinalIgnoreCase) ||
                item.ProductName.Contains(p.Name,
                    StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains(item.ProductName,
                    StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                await _whatsAppSendService.SendTextMessageAsync(fromPhone,
                    $"Sorry, I couldn't find '{item.ProductName}' in our catalogue. " +
                    $"Type 'browse' to see all available products.");
                return;
            }

            orderItems.Add(new OrderItemDto
            {
                ProductId = match.Id,
                Quantity = item.Quantity,
                NegotiatedPrice = item.OfferedPrice
            });
        }

        try
        {
            var createDto = new CreateOrderDto
            {
                CustomerId = customerId,
                Items = orderItems
            };

            var order = await _orderService.CreateOrderAsync(createDto);
            var confirmationMsg = _orderService.FormatOrderConfirmationMessage(order);

            await _whatsAppSendService.SendTextMessageAsync(fromPhone, confirmationMsg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order creation failed for {Phone}",
                MaskPhone(fromPhone));

            await _whatsAppSendService.SendTextMessageAsync(fromPhone,
                $"Sorry, I couldn't process your order: {ex.Message}");
        }
    }

    // 🔍 PRODUCT SEARCH HANDLER
    private async Task HandleProductSearchAsync(
        string fromPhone, string query, string language)
    {
        var products = await _productService.SmartSearchAsync(query);

        if (!products.Any())
        {
            var noResultMsg = language switch
            {
                "si" => "සමාවෙන්න, ඔබ සොයන භාණ්ඩය නොමැත. 'browse' ටයිප් කරන්න.",
                "ta" => "மன்னிக்கவும், தேடிய பொருள் கிடைக்கவில்லை. 'browse' என்று தட்டச்சு செய்யவும்.",
                _    => "Sorry, I couldn't find matching products. Type 'browse' to see all items."
            };

            await _whatsAppSendService.SendTextMessageAsync(fromPhone, noResultMsg);
            return;
        }

        var message = _productService.FormatProductsForWhatsApp(products);
        await _whatsAppSendService.SendTextMessageAsync(fromPhone, message);
    }

    // 👤 GET OR CREATE CUSTOMER
    private async Task<Customer> GetOrCreateCustomerAsync(string phone, string name)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.PhoneNumber == phone);

        if (customer == null)
        {
            customer = new Customer
            {
                PhoneNumber = phone,
                Name = name,
                CreatedAt = DateTime.UtcNow
            };

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();

            _logger.LogInformation("New customer created: {Phone}", MaskPhone(phone));
        }

        return customer;
    }

    // 💬 GET OR CREATE CONVERSATION
    private async Task<Conversation> GetOrCreateConversationAsync(int customerId)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                CustomerId = customerId,
                State = ConversationState.Greeting,
                LastMessageAt = DateTime.UtcNow
            };

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync();
        }

        if ((DateTime.UtcNow - conversation.LastMessageAt).TotalHours >= 2)
        {
            conversation.State = ConversationState.Greeting;
            conversation.Context = null;
        }

        return conversation;
    }

    // 🔒 MASK PHONE
    private static string MaskPhone(string phone)
    {
        if (phone.Length < 6) return "***";
        return phone[..6] + "***" + phone[^3..];
    }
}