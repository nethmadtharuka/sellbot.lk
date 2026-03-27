using SellBotLk.Api.Data;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace SellBotLk.Api.Services;

public class MessageProcessingService
{
    private readonly GeminiService _geminiService;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly ProductService _productService;
    private readonly VisualSearchService _visualSearchService;
    private readonly AppDbContext _db;
    private readonly ILogger<MessageProcessingService> _logger;

    public MessageProcessingService(
        GeminiService geminiService,
        WhatsAppSendService whatsAppSendService,
        ProductService productService,
        VisualSearchService visualSearchService,
        AppDbContext db,
        ILogger<MessageProcessingService> logger)
    {
        _geminiService = geminiService;
        _whatsAppSendService = whatsAppSendService;
        _productService = productService;
        _visualSearchService = visualSearchService;
        _db = db;
        _logger = logger;
    }

    // ✉️ HANDLE TEXT MESSAGES
    public async Task ProcessTextMessageAsync(
        string fromPhone, string messageText, string senderName)
    {
        // 1. Get or create customer
        var customer = await GetOrCreateCustomerAsync(fromPhone, senderName);

        // 2. Get or create conversation
        var conversation = await GetOrCreateConversationAsync(customer.Id);

        // 3. Build context
        var context = conversation.Context ?? "";

        _logger.LogInformation(
            "Processing message from {Phone} — State: {State}",
            MaskPhone(fromPhone), conversation.State);

        // 4. Call Gemini
        var parsed = await _geminiService.ParseMessageAsync(
            messageText,
            customer.Name ?? senderName,
            context);

        _logger.LogInformation(
            "Intent: {Intent} ({Confidence:P0}) — Language: {Lang}",
            parsed.Intent, parsed.Confidence, parsed.Language);

        // 5. Update language preference
        if (customer.PreferredLanguage != parsed.Language)
        {
            customer.PreferredLanguage = parsed.Language;
        }

        // 6. Update conversation state
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

        // 7. Route based on intent
        switch (parsed.Intent)
        {
            case "ProductSearch":
                await HandleProductSearchAsync(
                    fromPhone,
                    parsed.ProductSearchQuery ?? messageText,
                    parsed.Language);
                break;

            default:
                await _whatsAppSendService.SendTextMessageAsync(
                    fromPhone, parsed.ReplyMessage);
                break;
        }
    }

    // 🖼️ HANDLE IMAGE MESSAGES (Visual Search)
    public async Task ProcessImageMessageAsync(
        string fromPhone, byte[] imageBytes, string mimeType, string senderName)
    {
        var customer = await GetOrCreateCustomerAsync(fromPhone, senderName);
        var conversation = await GetOrCreateConversationAsync(customer.Id);

        _logger.LogInformation(
            "Processing image from {Phone} — running visual search",
            MaskPhone(fromPhone));

        // Let customer know we received the image
        await _whatsAppSendService.SendTextMessageAsync(fromPhone,
            "🔍 Analyzing your image, please wait...");

        // Run visual search via Gemini Vision
        var result = await _visualSearchService.SearchByImageAsync(imageBytes, mimeType);
        var message = _visualSearchService.FormatVisualSearchResultForWhatsApp(result);

        // Update conversation state
        conversation.State = ConversationState.Browsing;
        conversation.LastMessageAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _whatsAppSendService.SendTextMessageAsync(fromPhone, message);
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
    private async Task<Customer> GetOrCreateCustomerAsync(
        string phone, string name)
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

            _logger.LogInformation(
                "New customer created: {Phone}",
                MaskPhone(phone));
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

        // ⏱️ Reset stale conversations after 2 hours of inactivity
        if ((DateTime.UtcNow - conversation.LastMessageAt).TotalHours >= 2)
        {
            conversation.State = ConversationState.Greeting;
            conversation.Context = null;
        }

        return conversation;
    }

    // 🔒 MASK PHONE NUMBER FOR SAFE LOGGING
    private static string MaskPhone(string phone)
    {
        if (phone.Length < 6) return "***";
        return phone[..6] + "***" + phone[^3..];
    }
}