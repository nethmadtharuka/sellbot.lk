using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Services;

namespace SellBotLk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/delivery-zones")]
public class DeliveryZonesController : ControllerBase
{
    private readonly DeliveryService _deliveryService;
    private readonly ILogger<DeliveryZonesController> _logger;

    public DeliveryZonesController(
        DeliveryService deliveryService,
        ILogger<DeliveryZonesController> logger)
    {
        _deliveryService = deliveryService;
        _logger = logger;
    }

    /// <summary>Get all active delivery zones with fees and ETAs</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var zones = await _deliveryService.GetAllZonesAsync();
        return Ok(new
        {
            success = true,
            data = zones,
            count = zones.Count
        });
    }

    /// <summary>
    /// Check if an area is serviceable and get delivery fee for an order total.
    /// Used during checkout to show delivery cost to customer.
    /// </summary>
    [HttpPost("check")]
    public async Task<IActionResult> Check([FromBody] DeliveryCheckRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Area))
            return BadRequest(new { success = false,
                error = "Area is required" });

        var result = await _deliveryService.CheckZoneAsync(
            dto.Area, dto.OrderTotal);

        return Ok(new { success = true, data = result });
    }
}