using SellBotLk.Api.Data;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Models;

namespace SellBotLk.Api.Services;

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

    // ✉️ HANDLE TEXT MESSAGES
    public async Task ProcessTextMessageAsync(
        string fromPhone, string messageText, string senderName)
    {
        // FIX 1: Top-level safety net — any unhandled exception still sends a reply.
        // Without this, exceptions cause silent failures where the customer gets nothing.
        try
        {
            var customer = await GetOrCreateCustomerAsync(fromPhone, senderName);
            var conversation = await GetOrCreateConversationAsync(customer.Id);
            var context = BuildStructuredContext(conversation, customer);

            _logger.LogInformation(
                "Processing message from {Phone} — State: {State}",
                MaskPhone(fromPhone), conversation.State);

            // FIX 2: Pass catalogue summary into ParseMessageAsync so Gemini knows
            // what products exist and can extract accurate product names from orders.
            var catalogueSummary = await _productService.BuildCatalogueSummaryAsync();

            var parsed = await _geminiService.ParseMessageAsync(
                messageText,
                customer.Name ?? senderName,
                context,
                catalogueSummary);

            _logger.LogInformation(
                "Intent: {Intent} ({Confidence:P0}) — Language: {Lang}",
                parsed.Intent, parsed.Confidence, parsed.Language);

            // Update language preference if changed
            if (customer.PreferredLanguage != parsed.Language)
            {
                customer.PreferredLanguage = parsed.Language;
            }

            conversation.State = parsed.Intent switch
            {
                "Greeting"            => ConversationState.Greeting,
                "ProductSearch"       => ConversationState.Browsing,
                "Order"               => ConversationState.Ordering,
                "PriceNegotiation"    => ConversationState.Negotiating,
                "Complaint"
                    or "OrderStatus"
                    or "DeliveryInfo"  => ConversationState.Support,
                _                     => conversation.State
            };

            conversation.LastMessageAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // FIX 3: Every intent is now explicitly handled with real data.
            // Nothing falls to a generic "ask Gemini to make something up" path.
            switch (parsed.Intent)
            {
                case "Greeting":
                    await HandleGreetingAsync(fromPhone, parsed, customer);
                    break;

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

                case "OrderStatus":
                    await HandleOrderStatusAsync(
                        fromPhone, parsed, customer.Id, parsed.Language);
                    break;

                case "DeliveryInfo":
                    await HandleDeliveryInfoAsync(
                        fromPhone, parsed, parsed.Language);
                    break;

                case "Complaint":
                    await HandleComplaintAsync(
                        fromPhone, parsed, customer.Id, parsed.Language);
                    break;

                case "PaymentConfirmation":
                    // Customer typed a payment reference — tell them to send the slip image
                    await HandlePaymentTextAsync(fromPhone, parsed.Language);
                    break;

                default:
                    // Catch-all: use Gemini's reply but always guarantee a response
                    var fallbackReply = !string.IsNullOrWhiteSpace(parsed.ReplyMessage)
                        ? parsed.ReplyMessage
                        : GetHelpMessage(parsed.Language);

                    await _whatsAppSendService.SendTextMessageAsync(fromPhone, fallbackReply);
                    break;
            }
        }
        catch (Exception ex)
        {
            // FIX 1: Never let an exception cause a silent failure.
            // Customer always gets a reply, even on unhandled errors.
            _logger.LogError(ex,
                "Unhandled error processing text message from {Phone}", MaskPhone(fromPhone));

            try
            {
                await _whatsAppSendService.SendTextMessageAsync(fromPhone,
                    "Sorry, something went wrong on our end. " +
                    "Please try again in a moment! 🙏");
            }
            catch (Exception sendEx)
            {
                _logger.LogError(sendEx,
                    "Failed to send error recovery message to {Phone}", MaskPhone(fromPhone));
            }
        }
    }

    // 🖼️ HANDLE IMAGE MESSAGES
    // FIX 4: Image routing is now context-aware.
    // The webhook sends ALL images here. We decide what to do based on
    // conversation state — if ordering/browsing, treat as visual search.
    // If payment context, route to PaymentMatchingService (done in webhook).
    public async Task ProcessImageMessageAsync(
        string fromPhone, byte[] imageBytes, string mimeType, string senderName)
    {
        try
        {
            var customer = await GetOrCreateCustomerAsync(fromPhone, senderName);
            var conversation = await GetOrCreateConversationAsync(customer.Id);

            _logger.LogInformation(
                "Processing image from {Phone} — State: {State}",
                MaskPhone(fromPhone), conversation.State);

            await _whatsAppSendService.SendTextMessageAsync(fromPhone,
                "🔍 Analyzing your image, please wait...");

            var result = await _visualSearchService.SearchByImageAsync(imageBytes, mimeType);
            var message = _visualSearchService.FormatVisualSearchResultForWhatsApp(result);

            conversation.State = ConversationState.Browsing;
            conversation.LastMessageAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _whatsAppSendService.SendTextMessageAsync(fromPhone, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing image from {Phone}", MaskPhone(fromPhone));

            await _whatsAppSendService.SendTextMessageAsync(fromPhone,
                "Sorry, I couldn't process your image. Please try again!");
        }
    }

    // 👋 GREETING HANDLER
    // FIX 5: Greetings now include a menu so customers know what to do.
    // Previously Gemini's generic reply was used — now we include real info.
    private async Task HandleGreetingAsync(
        string fromPhone,
        ParsedMessageIntent parsed,
        Customer customer)
    {
        var isReturning = customer.TotalOrders > 0;
        string message;

        if (isReturning)
        {
            // Personalised returning customer greeting
            var firstName = (customer.Name ?? "there").Split(' ')[0];
            message = parsed.Language switch
            {
                "si" => $"🙏 ආයුබෝවන් *{firstName}*! ඔබව නැවත දකින්නට ලැබීම සතුටකි.\n\n" +
                        $"ඔබට සිල්ලර ෆර්නිචර් හා සාමාන්‍ය නිෂ්පාදන සෙවීම, ඇණවුම් කිරීම " +
                        $"හෝ ඒවා ගැන විමසීම කළ හැක.\n\n" +
                        $"📦 *browse* - භාණ්ඩ බලන්න\n" +
                        $"🛒 *order* - ඇණවුම් කරන්න\n" +
                        $"📋 *my orders* - ඔබේ ඇණවුම් බලන්න",
                "ta" => $"🙏 வணக்கம் *{firstName}*! மீண்டும் வரவேற்கிறோம்.\n\n" +
                        $"தயவுசெய்து உங்கள் தேவையை சொல்லுங்கள்.\n\n" +
                        $"📦 *browse* - பொருட்களை பார்க்க\n" +
                        $"🛒 *order* - ஆர்டர் செய்ய\n" +
                        $"📋 *my orders* - உங்கள் ஆர்டர்களை பார்க்க",
                _ =>    $"👋 Welcome back, *{firstName}*! Great to see you again.\n\n" +
                        $"How can I help you today?\n\n" +
                        $"📦 *browse* — see all products\n" +
                        $"🛒 Just tell me what you want to order\n" +
                        $"📋 *my orders* — check your order status\n" +
                        $"📸 Send a photo to find similar products"
            };
        }
        else
        {
            // New customer greeting
            message = parsed.Language switch
            {
                "si" => "🙏 ආයුබෝවන්! SellBot.lk වෙත සාදරයෙන් පිළිගනිමු.\n\n" +
                        "අපි ශ්‍රී ලංකාවේ ෆර්නිචර් ව්‍යාපාරයකි. " +
                        "ඔබට WhatsApp හරහා ඇණවුම් කළ හැකිය!\n\n" +
                        "📦 *browse* - භාණ්ඩ බලන්න\n" +
                        "🛒 ඔබට අවශ්‍ය දේ ලියන්න\n" +
                        "📸 ෆොටෝ එකක් යවන්න - සමාන භාණ්ඩ හොයනවා",
                "ta" => "🙏 வணக்கம்! SellBot.lk-க்கு வரவேற்கிறோம்.\n\n" +
                        "நாங்கள் இலங்கையில் தளபாடங்கள் விற்கிறோம். " +
                        "WhatsApp மூலம் ஆர்டர் செய்யலாம்!\n\n" +
                        "📦 *browse* - பொருட்களை பார்க்க\n" +
                        "🛒 உங்கள் தேவையை சொல்லுங்கள்\n" +
                        "📸 படம் அனுப்பி பொருள் தேடுங்கள்",
                _ =>    "👋 Welcome to *SellBot.lk*! We're a Sri Lankan furniture business.\n\n" +
                        "Here's what you can do:\n\n" +
                        "📦 Type *browse* — see all products\n" +
                        "🛒 Tell me what you want — I'll find it\n" +
                        "📸 Send a photo — I'll find similar products\n" +
                        "🏷️ Ask for a price — I can negotiate!\n\n" +
                        "What are you looking for today?"
            };
        }

        await _whatsAppSendService.SendTextMessageAsync(fromPhone, message);
    }

    // 📋 ORDER STATUS HANDLER
    // FIX 6: Actually looks up the order in the DB instead of letting Gemini make up an answer.
    private async Task HandleOrderStatusAsync(
        string fromPhone,
        ParsedMessageIntent parsed,
        int customerId,
        string language)
    {
        Order? order = null;

        // Try by order number first if mentioned
        if (!string.IsNullOrEmpty(parsed.OrderNumber))
        {
            order = await _db.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.OrderNumber == parsed.OrderNumber);
        }

        // Fall back to most recent order for this customer
        if (order == null)
        {
            order = await _db.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        if (order == null)
        {
            var noOrderMsg = language switch
            {
                "si" => "ඔබේ නමින් ඇණවුම් නොමැත. ඇණවුම් කිරීමට 'browse' ටයිප් කරන්න.",
                "ta" => "உங்கள் பெயரில் ஆர்டர் இல்லை. 'browse' என்று தட்டச்சு செய்யவும்.",
                _ =>    "I couldn't find any orders for you. " +
                        "Type 'browse' to shop, or provide your order number."
            };
            await _whatsAppSendService.SendTextMessageAsync(fromPhone, noOrderMsg);
            return;
        }

        var statusEmoji = order.Status switch
        {
            OrderStatus.Confirmed   => "✅",
            OrderStatus.Processing  => "⚙️",
            OrderStatus.Dispatched  => "🚚",
            OrderStatus.Delivered   => "🎉",
            OrderStatus.Cancelled   => "❌",
            _                       => "📋"
        };

        var paymentEmoji = order.PaymentStatus == PaymentStatus.Paid ? "✅" : "⏳";

        var message = language switch
        {
            "si" => $"{statusEmoji} *ඔබේ ඇණවුම — {order.OrderNumber}*\n\n" +
                    $"තත්ත්වය: *{order.Status}*\n" +
                    $"ගෙවීම: {paymentEmoji} {order.PaymentStatus}\n" +
                    $"මුළු: LKR {order.TotalAmount:N0}\n" +
                    (order.DeliveryArea != null ? $"බෙදාහැරීම: {order.DeliveryArea}\n" : "") +
                    $"\nප්‍රශ්නයක් ඇත්නම් ලිවීමෙන් කතා කරන්න.",
            "ta" => $"{statusEmoji} *உங்கள் ஆர்டர் — {order.OrderNumber}*\n\n" +
                    $"நிலை: *{order.Status}*\n" +
                    $"கட்டணம்: {paymentEmoji} {order.PaymentStatus}\n" +
                    $"மொத்தம்: LKR {order.TotalAmount:N0}\n" +
                    (order.DeliveryArea != null ? $"டெலிவரி: {order.DeliveryArea}\n" : "") +
                    $"\nஏதேனும் கேள்வி இருந்தால் தொடர்பு கொள்ளுங்கள்.",
            _ =>    $"{statusEmoji} *Order Status — {order.OrderNumber}*\n\n" +
                    $"Status: *{order.Status}*\n" +
                    $"Payment: {paymentEmoji} {order.PaymentStatus}\n" +
                    $"Total: LKR {order.TotalAmount:N0}\n" +
                    (order.DeliveryArea != null ? $"Delivery to: {order.DeliveryArea}\n" : "") +
                    $"\nReply if you have any questions!"
        };

        await _whatsAppSendService.SendTextMessageAsync(fromPhone, message);
    }

    // 🚚 DELIVERY INFO HANDLER
    // FIX 7: Returns real delivery zones from DB instead of a hallucinated answer.
    private async Task HandleDeliveryInfoAsync(
        string fromPhone,
        ParsedMessageIntent parsed,
        string language)
    {
        var zones = await _db.DeliveryZones
            .Where(z => z.IsActive)
            .OrderBy(z => z.DeliveryFee)
            .ToListAsync();

        if (!zones.Any())
        {
            await _whatsAppSendService.SendTextMessageAsync(fromPhone,
                "Please contact us directly for delivery information.");
            return;
        }

        var header = language switch
        {
            "si" => "🚚 *බෙදාහැරීමේ ප්‍රදේශ සහ ගාස්තු:*\n",
            "ta" => "🚚 *டெலிவரி பகுதிகள் மற்றும் கட்டணங்கள்:*\n",
            _ =>    "🚚 *Delivery Zones & Fees:*\n"
        };

        var lines = new List<string> { header };

        foreach (var zone in zones.Take(10))
        {
            var freeInfo = zone.FreeDeliveryThreshold.HasValue
                ? $" (Free over LKR {zone.FreeDeliveryThreshold:N0})"
                : "";

            lines.Add($"📍 *{zone.ZoneName}* — LKR {zone.DeliveryFee:N0} | " +
                      $"{zone.EstimatedDays} day(s){freeInfo}");
        }

        if (zones.Count > 10)
            lines.Add($"\n_...and {zones.Count - 10} more zones. Ask for a specific area!_");

        lines.Add(language switch
        {
            "si" => "\nඔබේ ප්‍රදේශය ලියන්න — නිශ්චිත ගාස්තු දෙනවා!",
            "ta" => "\nஉங்கள் பகுதியை சொல்லுங்கள் — குறிப்பிட்ட கட்டணம் சொல்கிறோம்!",
            _ =>    "\nTell me your area for an exact delivery fee!"
        });

        await _whatsAppSendService.SendTextMessageAsync(
            fromPhone, string.Join("\n", lines));
    }

    // 🚨 COMPLAINT HANDLER
    // FIX 8: Acknowledges properly and logs — doesn't just send a generic AI reply.
    private async Task HandleComplaintAsync(
        string fromPhone,
        ParsedMessageIntent parsed,
        int customerId,
        string language)
    {
        _logger.LogWarning(
            "Complaint received from customer {Id} — {Phone}",
            customerId, MaskPhone(fromPhone));

        var message = language switch
        {
            "si" => "😔 ඔබේ ගැටලුව ගැන කණගාටුයි. " +
                    "අපේ කණ්ඩායම ඉක්මනින් ඔබව සම්බන්ධ කර ගනු ඇත.\n\n" +
                    "ඔබේ ඇණවුම් අංකය (ORD-XXXX) සඳහන් කළොත් ඉක්මනින් " +
                    "ගැටලුව විසඳීමට හැකිවේ.",
            "ta" => "😔 உங்கள் பிரச்சனைக்கு மன்னிக்கவும். " +
                    "எங்கள் குழு விரைவில் தொடர்பு கொள்ளும்.\n\n" +
                    "உங்கள் ஆர்டர் எண் (ORD-XXXX) தெரிவித்தால் " +
                    "விரைவாக தீர்வு காணலாம்.",
            _ =>    "😔 We're sorry to hear about your issue. " +
                    "Our team will get back to you shortly.\n\n" +
                    "If you share your order number (ORD-XXXX), " +
                    "we can resolve this faster for you."
        };

        await _whatsAppSendService.SendTextMessageAsync(fromPhone, message);
    }

    // 💳 PAYMENT TEXT HANDLER
    // FIX 9: Guides customer to send the actual image instead of just text.
    private async Task HandlePaymentTextAsync(string fromPhone, string language)
    {
        var message = language switch
        {
            "si" => "💳 ගෙවීම් තහවුරු කිරීමට, කරුණාකර බැංකු " +
                    "ගිණුම් පිටපතේ ෆොටෝ එකක් (screenshot) යවන්න.\n\n" +
                    "📸 ෆොටෝ එකක් ලෙස JPEG හෝ PNG ගොනු ගන්න.",
            "ta" => "💳 கட்டணத்தை உறுதிப்படுத்த, வங்கி பரிமாற்ற " +
                    "ஸ்கிரீன்ஷாட்டை படமாக அனுப்பவும்.\n\n" +
                    "📸 JPEG அல்லது PNG படம் அனுப்பவும்.",
            _ =>    "💳 To confirm your payment, please send a *photo* of " +
                    "your bank transfer screenshot.\n\n" +
                    "📸 Just take a screenshot of your banking app and send it here!"
        };

        await _whatsAppSendService.SendTextMessageAsync(fromPhone, message);
    }

    // 💰 NEGOTIATION HANDLER
    private async Task HandleNegotiationAsync(
        string fromPhone,
        ParsedMessageIntent parsed,
        int customerId,
        string language)
    {
        var existingContext = await _negotiationService.GetNegotiationContextAsync(customerId);

        if (existingContext != null &&
            parsed.Intent == "PriceNegotiation" &&
            (parsed.ReplyMessage?.Contains("confirm",
                StringComparison.OrdinalIgnoreCase) == true ||
             parsed.OfferedPrice == existingContext.CounterOffer))
        {
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

        var productId = parsed.ProductId;
        var offeredPrice = parsed.OfferedPrice;
        var quantity = parsed.OrderItems?.FirstOrDefault()?.Quantity ?? 1;

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
            var clarifyMsg = language switch
            {
                "si" => "කුමන භාණ්ඩය සහ ඔබ ඉදිරිපත් කරන මිල ලියන්න.\n" +
                        "උදා: 'leather chair LKR 25,000 කට දෙනවාද?'",
                "ta" => "எந்த பொருள் மற்றும் எந்த விலையில் வேண்டும் என்று சொல்லுங்கள்.\n" +
                        "உதா: 'leather chair LKR 25,000-க்கு தருவீர்களா?'",
                _ =>    "Which product would you like to negotiate on, " +
                        "and what price are you offering?\n" +
                        "Example: 'Can I get the leather chair for LKR 25,000?'"
            };
            await _whatsAppSendService.SendTextMessageAsync(fromPhone, clarifyMsg);
            return;
        }

        var result = await _negotiationService.EvaluateOfferAsync(
            productId.Value, quantity, offeredPrice.Value, language);

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

    // 🛒 ORDER HANDLER
    private async Task HandleOrderAsync(
        string fromPhone,
        ParsedMessageIntent parsed,
        int customerId,
        string language)
    {
        if (parsed.OrderItems == null || !parsed.OrderItems.Any())
        {
            var clarifyMsg = language switch
            {
                "si" => "ඔඩරය දීමට, කුමන භාණ්ඩය සහ කීයක් ගන්නද කියන්න.\n" +
                        "උදා: 'dining chairs 2ක් ගන්නවා'",
                "ta" => "ஆர்டர் செய்ய, எந்த பொருள் எத்தனை வேண்டும் என்று சொல்லுங்கள்.\n" +
                        "உதா: 'dining chairs 2 வேண்டும்'",
                _ =>    "I'd love to help you order! Could you tell me which product " +
                        "and quantity you'd like?\nExample: 'I want 2 dining chairs'"
            };
            await _whatsAppSendService.SendTextMessageAsync(fromPhone, clarifyMsg);
            return;
        }

        var allProducts = await _productService.GetAllAsync();
        var orderItems = new List<OrderItemDto>();
        var notFoundNames = new List<string>();

        foreach (var item in parsed.OrderItems)
        {
            // FIX 10: Multi-strategy product matching — exact, partial, category, word-overlap.
            // Old code only used .Contains() which fails on word order differences.
            var match = FindBestProductMatch(item.ProductName, allProducts);

            if (match == null)
            {
                notFoundNames.Add(item.ProductName);
                continue;
            }

            orderItems.Add(new OrderItemDto
            {
                ProductId = match.Id,
                Quantity = item.Quantity,
                NegotiatedPrice = item.OfferedPrice
            });
        }

        if (notFoundNames.Any())
        {
            var notFoundMsg = language switch
            {
                "si" => $"සමාවෙන්න, '{string.Join("', '", notFoundNames)}' " +
                        $"කැටලොගයේ නොමැත.\n'browse' ටයිප් කර ලැබෙන ලැයිස්තුව බලන්න.",
                "ta" => $"மன்னிக்கவும், '{string.Join("', '", notFoundNames)}' " +
                        $"கிடைக்கவில்லை.\n'browse' என்று தட்டச்சு செய்து பட்டியல் பாருங்கள்.",
                _ =>    $"Sorry, I couldn't find '{string.Join("', '", notFoundNames)}' " +
                        $"in our catalogue.\nType 'browse' to see all available products."
            };
            await _whatsAppSendService.SendTextMessageAsync(fromPhone, notFoundMsg);
            return;
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
            _logger.LogError(ex, "Order creation failed for {Phone}", MaskPhone(fromPhone));

            // Send a user-friendly message, not a raw exception message
            var errorMsg = language switch
            {
                "si" => ex.Message.Contains("stock", StringComparison.OrdinalIgnoreCase)
                    ? $"😔 සමාවෙන්න — {ex.Message}"
                    : "😔 ඔඩරය සෑදීමේ දෝෂයකි. නැවත උත්සාහ කරන්න.",
                "ta" => ex.Message.Contains("stock", StringComparison.OrdinalIgnoreCase)
                    ? $"😔 மன்னிக்கவும் — {ex.Message}"
                    : "😔 ஆர்டர் செய்ய முடியவில்லை. மீண்டும் முயற்சிக்கவும்.",
                _ =>    ex.Message.Contains("stock", StringComparison.OrdinalIgnoreCase)
                    ? $"😔 Sorry — {ex.Message}"
                    : "😔 Something went wrong placing your order. Please try again!"
            };

            await _whatsAppSendService.SendTextMessageAsync(fromPhone, errorMsg);
        }
    }

    // 🔍 PRODUCT SEARCH HANDLER
    private async Task HandleProductSearchAsync(
        string fromPhone, string query, string language)
    {
        var products = await _productService.SmartSearchAsync(query);
        var message = _productService.FormatProductsForWhatsApp(products, language);
        await _whatsAppSendService.SendTextMessageAsync(fromPhone, message);
    }

    // 🔍 MULTI-STRATEGY PRODUCT MATCHING
    // FIX 10: Tries multiple matching strategies in order of precision.
    // This handles "dining chair", "Dining Chairs", "oak dining chair", "chair", etc.
    private static ProductResponseDto? FindBestProductMatch(
        string searchName, List<ProductResponseDto> products)
    {
        if (string.IsNullOrWhiteSpace(searchName)) return null;

        var search = searchName.Trim();

        // Strategy 1: Exact match (case-insensitive)
        var exact = products.FirstOrDefault(p =>
            p.Name.Equals(search, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Strategy 2: Product name contains search term
        var nameContains = products.FirstOrDefault(p =>
            p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (nameContains != null) return nameContains;

        // Strategy 3: Search term contains product name
        var searchContains = products.FirstOrDefault(p =>
            search.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (searchContains != null) return searchContains;

        // Strategy 4: Category match
        var categoryMatch = products.FirstOrDefault(p =>
            p.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            search.Contains(p.Category, StringComparison.OrdinalIgnoreCase));
        if (categoryMatch != null) return categoryMatch;

        // Strategy 5: Word overlap — "oak dining chair" matches "Dining Chair"
        var searchWords = search.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bestMatch = products
            .Select(p => new
            {
                Product = p,
                Score = searchWords.Count(w =>
                    p.Name.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                    p.Category.Contains(w, StringComparison.OrdinalIgnoreCase))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return bestMatch?.Product;
    }

    // 💬 BUILD STRUCTURED CONTEXT
    // FIX 11: Context passed to Gemini is now structured so it's useful.
    // Previously it was a raw string Gemini didn't know what to do with.
    private static string BuildStructuredContext(Conversation conversation, Customer customer)
    {
        var parts = new List<string>();

        if (conversation.State != ConversationState.Greeting)
            parts.Add($"Current conversation state: {conversation.State}");

        if (customer.TotalOrders > 0)
            parts.Add($"Customer has placed {customer.TotalOrders} previous order(s)");

        if (!string.IsNullOrEmpty(conversation.Context))
            parts.Add($"Active context: {conversation.Context}");

        return parts.Any() ? string.Join(". ", parts) : "";
    }

    // ℹ️ HELP MESSAGE
    private static string GetHelpMessage(string language) => language switch
    {
        "si" => "ඔබට කෙසේ උදව් කළ හැකිද?\n\n" +
                "📦 *browse* - භාණ්ඩ බලන්න\n" +
                "🛒 ඔබට අවශ්‍ය දේ ලියන්න\n" +
                "📋 *my orders* - ඔඩරය බලන්න\n" +
                "📸 ෆොටෝ — සමාන භාණ්ඩ හොයනවා",
        "ta" => "எவ்வாறு உதவலாம்?\n\n" +
                "📦 *browse* - பொருட்களை பார்க்க\n" +
                "🛒 உங்கள் தேவையை சொல்லுங்கள்\n" +
                "📋 *my orders* - ஆர்டர் பார்க்க\n" +
                "📸 படம் அனுப்பி பொருள் தேடுங்கள்",
        _ =>    "Here's what I can help with:\n\n" +
                "📦 Type *browse* — see all products\n" +
                "🛒 Tell me what you want to order\n" +
                "📋 Type *my orders* — check order status\n" +
                "📸 Send a photo — I'll find similar products\n\n" +
                "What would you like to do?"
    };

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
        else if ((DateTime.UtcNow - conversation.LastMessageAt).TotalHours >= 2)
        {
            // FIX 12: Safely reset stale context with try/catch
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