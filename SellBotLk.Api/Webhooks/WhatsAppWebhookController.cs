using Microsoft.AspNetCore.Mvc;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Services;

namespace SellBotLk.Api.Webhooks;

[ApiController]
[Route("api/v1/webhook")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly MessageProcessingService _messageProcessingService;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IConfiguration config,
        WhatsAppSendService whatsAppSendService,
        MessageProcessingService messageProcessingService,
        ILogger<WhatsAppWebhookController> logger)
    {
        _config = config;
        _whatsAppSendService = whatsAppSendService;
        _messageProcessingService = messageProcessingService;
        _logger = logger;
    }

    [HttpGet("whatsapp")]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string token,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var verifyToken = _config["WhatsApp:VerifyToken"];

        if (mode == "subscribe" && token == verifyToken)
        {
            _logger.LogInformation("WhatsApp webhook verified successfully");
            return Ok(challenge);
        }

        _logger.LogWarning("WhatsApp webhook verification failed — token mismatch");
        return Unauthorized();
    }

    [HttpPost("whatsapp")]
    public async Task<IActionResult> Receive([FromBody] WhatsAppIncomingDto payload)
    {
        try
        {
            var messages = payload.Entry
                .SelectMany(e => e.Changes)
                .Select(c => c.Value)
                .SelectMany(v => v.Messages ?? new List<WhatsAppMessageItemDto>())
                .ToList();

            var contacts = payload.Entry
                .SelectMany(e => e.Changes)
                .Select(c => c.Value)
                .SelectMany(v => v.Contacts ?? new List<WhatsAppContactDto>())
                .ToList();

            foreach (var message in messages)
            {
                var senderName = contacts
                    .FirstOrDefault(c => c.WaId == message.From)
                    ?.Profile.Name ?? "Unknown";

                _logger.LogInformation(
                    "Message received from {Phone} ({Name}) — Type: {Type}",
                    MaskPhone(message.From), senderName, message.Type);

                await ProcessMessageAsync(message, senderName);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook payload");
            return Ok();
        }
    }

    private async Task ProcessMessageAsync(
        WhatsAppMessageItemDto message, string senderName)
    {
        switch (message.Type)
        {
            case "text":
                var text = message.Text?.Body ?? "";
                await _messageProcessingService.ProcessTextMessageAsync(
                    message.From, text, senderName);
                break;

            case "image":
                _logger.LogInformation("Image received — Media ID: {Id}",
                    message.Image?.Id);
                await _whatsAppSendService.SendTextMessageAsync(
                    message.From,
                    "Image received! Visual search coming in Sprint 5.");
                break;

            case "audio":
                _logger.LogInformation("Audio received — Media ID: {Id}",
                    message.Audio?.Id);
                await _whatsAppSendService.SendTextMessageAsync(
                    message.From,
                    "Voice note received! Audio processing coming in Sprint 14.");
                break;

            case "document":
                _logger.LogInformation("Document received — Media ID: {Id}",
                    message.Document?.Id);
                await _whatsAppSendService.SendTextMessageAsync(
                    message.From,
                    "Document received! Invoice processing coming in Sprint 9.");
                break;

            default:
                _logger.LogInformation("Unhandled message type: {Type}",
                    message.Type);
                break;
        }
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 6) return "***";
        return phone[..6] + "***" + phone[^3..];
    }
}