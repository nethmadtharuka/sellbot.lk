using SellBotLk.Api.Data;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace SellBotLk.Api.Services;

public class MessageProcessingService
{
    private readonly GeminiService _geminiService;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly AppDbContext _db;
    private readonly ILogger<MessageProcessingService> _logger;

    public MessageProcessingService(
        GeminiService geminiService,
        WhatsAppSendService whatsAppSendService,
        AppDbContext db,
        ILogger<MessageProcessingService> logger)
    {
        _geminiService = geminiService;
        _whatsAppSendService = whatsAppSendService;
        _db = db;
        _logger = logger;
    }

    public async Task ProcessTextMessageAsync(
        string fromPhone, string messageText, string senderName)
    {
        // Get or create customer
        var customer = await GetOrCreateCustomerAsync(fromPhone, senderName);

        // Get or create conversation
        var conversation = await GetOrCreateConversationAsync(customer.Id);

        // Build context from conversation
        var context = conversation.Context ?? "";

        _logger.LogInformation(
            "Processing message from {Phone} — State: {State}",
            MaskPhone(fromPhone), conversation.State);

        // Call Gemini to parse intent
        var parsed = await _geminiService.ParseMessageAsync(
            messageText,
            customer.Name ?? senderName,
            context);

        _logger.LogInformation(
            "Intent detected: {Intent} ({Confidence:P0}) — Language: {Lang}",
            parsed.Intent, parsed.Confidence, parsed.Language);

        // Update customer's preferred language if detected
        if (customer.PreferredLanguage != parsed.Language)
        {
            customer.PreferredLanguage = parsed.Language;
            await _db.SaveChangesAsync();
        }

        // Update conversation state based on intent
        conversation.State = parsed.Intent switch
        {
            "Greeting" => ConversationState.Greeting,
            "ProductSearch" => ConversationState.Browsing,
            "Order" => ConversationState.Ordering,
            "PriceNegotiation" => ConversationState.Negotiating,
            "Complaint" or "OrderStatus" or "DeliveryInfo" => ConversationState.Support,
            _ => conversation.State
        };

        conversation.LastMessageAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Send Gemini's reply back to customer
        await _whatsAppSendService.SendTextMessageAsync(
            fromPhone, parsed.ReplyMessage);
    }

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

            _logger.LogInformation("New customer created: {Phone}", MaskPhone(phone));
        }

        return customer;
    }

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

        // Reset stale conversations (2+ hours inactive)
        if ((DateTime.UtcNow - conversation.LastMessageAt).TotalHours >= 2)
        {
            conversation.State = ConversationState.Greeting;
            conversation.Context = null;
        }

        return conversation;
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 6) return "***";
        return phone[..6] + "***" + phone[^3..];
    }
}