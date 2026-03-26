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
    private readonly AppDbContext _db;
    private readonly ILogger<MessageProcessingService> _logger;

    public MessageProcessingService(
        GeminiService geminiService,
        WhatsAppSendService whatsAppSendService,
        ProductService productService,
        AppDbContext db,
        ILogger<MessageProcessingService> logger)
    {
        _geminiService = geminiService;
        _whatsAppSendService = whatsAppSendService;
        _productService = productService;
        _db = db;
        _logger = logger;
    }

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

        // 5. Update language
        if (customer.PreferredLanguage != parsed.Language)
        {
            customer.PreferredLanguage = parsed.Language;
        }

        // 6. Update conversation state
        conversation.State = parsed.Intent switch
        {
            "Greeting" => ConversationState.Greeting,
            "ProductSearch" => ConversationState.Browsing,
            "Order" => ConversationState.Ordering,
            "PriceNegotiation" => ConversationState.Negotiating,
            "Complaint" or "OrderStatus" or "DeliveryInfo"
                => ConversationState.Support,
            _ => conversation.State
        };

        conversation.LastMessageAt = DateTime.UtcNow;

        // 🔥 SAVE CHANGES (IMPORTANT)
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
                _ => "Sorry, I couldn't find matching products. Type 'browse' to see all items."
            };

            await _whatsAppSendService.SendTextMessageAsync(fromPhone, noResultMsg);
            return;
        }

        var message = _productService.FormatProductsForWhatsApp(products);

        await _whatsAppSendService.SendTextMessageAsync(fromPhone, message);
    }

    // 👤 CUSTOMER HANDLING
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

    // 💬 CONVERSATION HANDLING
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

        // ⏱ Reset stale conversations (after 2 hours)
        if ((DateTime.UtcNow - conversation.LastMessageAt).TotalHours >= 2)
        {
            conversation.State = ConversationState.Greeting;
            conversation.Context = null;
        }

        return conversation;
    }

    // 🔒 MASK PHONE (LOGGING SAFETY)
    private static string MaskPhone(string phone)
    {
        if (phone.Length < 6) return "***";
        return phone[..6] + "***" + phone[^3..];
    }
}