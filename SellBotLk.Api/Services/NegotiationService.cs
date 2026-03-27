using System.Text.Json;
using SellBotLk.Api.Data;
using SellBotLk.Api.Models;
using SellBotLk.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace SellBotLk.Api.Services;

public class NegotiationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NegotiationService> _logger;

    // Within 10% below MinPrice → counteroffer at MinPrice
    private const double CounterOfferThresholdPercent = 0.10;

    public NegotiationService(
        AppDbContext db,
        ILogger<NegotiationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates a customer's price offer against the product's MinPrice.
    /// Returns the negotiation result with the appropriate response message.
    /// </summary>
    public async Task<NegotiationResult> EvaluateOfferAsync(
        int productId,
        int quantity,
        decimal customerOffer,
        string language)
    {
        var product = await _db.Products.FindAsync(productId);

        if (product == null)
            return new NegotiationResult
            {
                Outcome = NegotiationOutcome.ProductNotFound,
                Message = "Sorry, I couldn't find that product."
            };

        // Check if bulk discount applies first
        if (quantity >= product.BulkMinQty && product.BulkDiscountPercent > 0)
        {
            var bulkPrice = product.Price * (1 - product.BulkDiscountPercent / 100m);

            // If customer offer is at or above bulk price, accept it
            if (customerOffer >= bulkPrice)
                return BuildAcceptResult(product, quantity, bulkPrice, language,
                    isBulk: true);
        }

        _logger.LogInformation(
            "Evaluating offer — Product: {Name}, Offer: {Offer}, MinPrice: {Min}",
            product.Name, customerOffer, product.MinPrice);

        // ── Negotiation logic ────────────────────────────────────────────

        // Offer meets or exceeds MinPrice → Accept
        if (customerOffer >= product.MinPrice)
            return BuildAcceptResult(product, quantity, customerOffer, language);

        // Offer is within 10% below MinPrice → Counteroffer at MinPrice
        var threshold = product.MinPrice * (1 - (decimal)CounterOfferThresholdPercent);
        if (customerOffer >= threshold)
            return BuildCounterResult(product, quantity, language);

        // Offer is too low → Reject politely
        return BuildRejectResult(product, language);
    }

    /// <summary>
    /// Saves the negotiation state to Conversation.Context so
    /// multi-turn negotiation persists across messages.
    /// </summary>
    public async Task SaveNegotiationContextAsync(
        int customerId,
        NegotiationContext context)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (conversation == null) return;

        conversation.Context = JsonSerializer.Serialize(context);
        conversation.State = ConversationState.Negotiating;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Loads the current negotiation context from Conversation.Context.
    /// Returns null if no active negotiation exists.
    /// </summary>
    public async Task<NegotiationContext?> GetNegotiationContextAsync(int customerId)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (conversation?.Context == null) return null;

        try
        {
            return JsonSerializer.Deserialize<NegotiationContext>(
                conversation.Context);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clears the negotiation context after resolution.
    /// </summary>
    public async Task ClearNegotiationContextAsync(int customerId)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (conversation == null) return;

        conversation.Context = null;
        conversation.State = ConversationState.Greeting;
        await _db.SaveChangesAsync();
    }

    // ── Result builders ──────────────────────────────────────────────────

    private static NegotiationResult BuildAcceptResult(
        Product product, int quantity, decimal acceptedPrice,
        string language, bool isBulk = false)
    {
        var lineTotal = acceptedPrice * quantity;
        var savings = (product.Price - acceptedPrice) * quantity;

        var message = language switch
        {
            "si" => $"✅ හරි! {product.Name} x{quantity} — " +
                    $"LKR {acceptedPrice:N0} බැගින් එකඟ වෙමු.\n" +
                    $"💰 මුළු: LKR {lineTotal:N0}" +
                    (savings > 0 ? $"\n🏷️ ඔබ LKR {savings:N0} ඉතිරි කළා!" : ""),
            "ta" => $"✅ சரி! {product.Name} x{quantity} — " +
                    $"LKR {acceptedPrice:N0} என்று ஒப்புக்கொள்கிறோம்.\n" +
                    $"💰 மொத்தம்: LKR {lineTotal:N0}" +
                    (savings > 0 ? $"\n🏷️ நீங்கள் LKR {savings:N0} சேமித்தீர்கள்!" : ""),
            _ =>    $"✅ Deal! {product.Name} x{quantity} " +
                    $"at LKR {acceptedPrice:N0} each.\n" +
                    $"💰 Total: LKR {lineTotal:N0}" +
                    (savings > 0 ? $"\n🏷️ You saved LKR {savings:N0}!" : "") +
                    (isBulk ? "\n📦 Bulk discount applied!" : "") +
                    "\n\nReply *confirm* to place the order."
        };

        return new NegotiationResult
        {
            Outcome = NegotiationOutcome.Accepted,
            AcceptedPrice = acceptedPrice,
            ProductId = product.Id,
            Quantity = quantity,
            Message = message
        };
    }

    private static NegotiationResult BuildCounterResult(
        Product product, int quantity, string language)
    {
        var counterPrice = product.MinPrice;
        var lineTotal = counterPrice * quantity;

        var message = language switch
        {
            "si" => $"🤝 {product.Name} සඳහා LKR {counterPrice:N0} " +
                    $"දෙන්නම් — මෙය අපට හැකි අඩුම මිලයි.\n" +
                    $"💰 මුළු: LKR {lineTotal:N0}\n" +
                    $"*confirm* ලියා ඔඩරය දෙන්න.",
            "ta" => $"🤝 {product.Name} க்கு LKR {counterPrice:N0} " +
                    $"தருகிறோம் — இது எங்கள் குறைந்த விலை.\n" +
                    $"💰 மொத்தம்: LKR {lineTotal:N0}\n" +
                    $"*confirm* என்று தட்டச்சு செய்து ஆர்டர் கொடுங்கள்.",
            _ =>    $"🤝 Best we can do for {product.Name} is " +
                    $"LKR {counterPrice:N0} each — that's our lowest price.\n" +
                    $"💰 Total for x{quantity}: LKR {lineTotal:N0}\n" +
                    $"Reply *confirm* to accept this price."
        };

        return new NegotiationResult
        {
            Outcome = NegotiationOutcome.CounterOffer,
            CounterOfferPrice = counterPrice,
            ProductId = product.Id,
            Quantity = quantity,
            Message = message
        };
    }

    private static NegotiationResult BuildRejectResult(
        Product product, string language)
    {
        var message = language switch
        {
            "si" => $"😔 සමාවෙන්න, {product.Name} සඳහා " +
                    $"LKR {product.Price:N0} අපේ සාමාන්‍ය මිලයි.\n" +
                    $"ඊට වඩා අඩු කිරීමට නොහැකිය.\n" +
                    $"ඔඩරය දිගටම කරගෙන යනවාද?",
            "ta" => $"😔 மன்னிக்கவும், {product.Name} க்கு " +
                    $"LKR {product.Price:N0} எங்கள் விலை.\n" +
                    $"இதை குறைக்க இயலாது.\n" +
                    $"ஆர்டர் தொடர வேண்டுமா?",
            _ =>    $"😔 Sorry, LKR {product.Price:N0} is our standard " +
                    $"price for {product.Name} and we're unable to go lower.\n" +
                    $"Would you like to order at the regular price?"
        };

        return new NegotiationResult
        {
            Outcome = NegotiationOutcome.Rejected,
            Message = message
        };
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

public class NegotiationResult
{
    public NegotiationOutcome Outcome { get; set; }
    public decimal? AcceptedPrice { get; set; }
    public decimal? CounterOfferPrice { get; set; }
    public int? ProductId { get; set; }
    public int Quantity { get; set; }
    public string Message { get; set; } = "";
}

public enum NegotiationOutcome
{
    Accepted,
    CounterOffer,
    Rejected,
    ProductNotFound
}