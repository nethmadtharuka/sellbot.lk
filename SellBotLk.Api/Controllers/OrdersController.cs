using Microsoft.AspNetCore.Mvc;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Services;

namespace SellBotLk.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        OrderService orderService,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>Get all orders with optional filters</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] int? customerId = null)
    {
        var orders = await _orderService.GetAllAsync(status, customerId);
        return Ok(new { success = true, data = orders, count = orders.Count });
    }

    /// <summary>Get a single order by ID</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var order = await _orderService.GetByIdAsync(id);
        if (order == null)
            return NotFound(new { success = false,
                error = $"Order {id} not found" });

        return Ok(new { success = true, data = order });
    }

    /// <summary>Get order by order number</summary>
    [HttpGet("number/{orderNumber}")]
    public async Task<IActionResult> GetByOrderNumber(string orderNumber)
    {
        var order = await _orderService.GetByOrderNumberAsync(orderNumber);
        if (order == null)
            return NotFound(new { success = false,
                error = $"Order {orderNumber} not found" });

        return Ok(new { success = true, data = order });
    }

    /// <summary>Manually create an order from admin dashboard</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, errors = ModelState });

        try
        {
            var order = await _orderService.CreateOrderAsync(dto);
            return CreatedAtAction(nameof(GetById),
                new { id = order.Id },
                new { success = true, data = order });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>Update order status</summary>
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id, [FromBody] UpdateOrderStatusDto dto)
    {
        try
        {
            var order = await _orderService.UpdateStatusAsync(id, dto.Status);
            if (order == null)
                return NotFound(new { success = false,
                    error = $"Order {id} not found" });

            return Ok(new { success = true, data = order });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>Cancel an order and restore stock</summary>
    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            var order = await _orderService.CancelOrderAsync(id);
            if (order == null)
                return NotFound(new { success = false,
                    error = $"Order {id} not found" });

            return Ok(new { success = true, data = order,
                message = "Order cancelled and stock restored." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}