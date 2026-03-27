using Microsoft.AspNetCore.Mvc;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Services;
using System.Text.Json;

namespace SellBotLk.Api.Webhooks;

[ApiController]
[Route("api/v1/webhook")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly MessageProcessingService _messageProcessingService;
    private readonly MediaDownloadService _mediaDownloadService;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IConfiguration config,
        WhatsAppSendService whatsAppSendService,
        MessageProcessingService messageProcessingService,
        MediaDownloadService mediaDownloadService,
        ILogger<WhatsAppWebhookController> logger)
    {
        _config = config;
        _whatsAppSendService = whatsAppSendService;
        _messageProcessingService = messageProcessingService;
        _mediaDownloadService = mediaDownloadService;
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
                await HandleImageMessageAsync(message, senderName);
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

    // 🖼️ IMAGE MESSAGE HANDLER
    private async Task HandleImageMessageAsync(
        WhatsAppMessageItemDto message, string senderName)
    {
        var mediaId = message.Image?.Id;

        if (string.IsNullOrEmpty(mediaId))
        {
            _logger.LogWarning("Image message received but no media ID found");
            await _whatsAppSendService.SendTextMessageAsync(
                message.From,
                "Sorry, I couldn't read your image. Please try sending it again.");
            return;
        }

        _logger.LogInformation(
            "Image received from {Phone} — Media ID: {Id}",
            MaskPhone(message.From), mediaId);

        try
        {
            // Step 1 — Download the image from Meta
            var (imageBytes, mimeType) = await _mediaDownloadService
                .DownloadMediaAsync(mediaId);

            if (imageBytes == null || imageBytes.Length == 0)
            {
                await _whatsAppSendService.SendTextMessageAsync(
                    message.From,
                    "Sorry, I couldn't download your image. Please try again.");
                return;
            }

            _logger.LogInformation(
                "Image downloaded — {Size} bytes, type: {MimeType}",
                imageBytes.Length, mimeType);

            // Step 2 — Run visual search
            await _messageProcessingService.ProcessImageMessageAsync(
                message.From, imageBytes, mimeType, senderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image from {Phone}",
                MaskPhone(message.From));

            await _whatsAppSendService.SendTextMessageAsync(
                message.From,
                "Sorry, I had trouble analyzing your image. " +
                "Please try again or describe what you're looking for in text.");
        }
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 6) return "***";
        return phone[..6] + "***" + phone[^3..];
    }
}