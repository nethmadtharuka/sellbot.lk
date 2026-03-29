namespace SellBotLk.Api.Models.DTOs;

public class DeliveryZoneResponseDto
{
    public int Id { get; set; }
    public string ZoneName { get; set; } = null!;
    public decimal DeliveryFee { get; set; }
    public int EstimatedDays { get; set; }
    public decimal? FreeDeliveryThreshold { get; set; }
    public bool IsActive { get; set; }
}

public class DeliveryCheckRequestDto
{
    public string Area { get; set; } = null!;
    public decimal OrderTotal { get; set; }
}

public class DeliveryCheckResponseDto
{
    public bool IsServiceable { get; set; }
    public string ZoneName { get; set; } = null!;
    public decimal DeliveryFee { get; set; }
    public int EstimatedDays { get; set; }
    public bool IsFreeDelivery { get; set; }
    public string Message { get; set; } = null!;
}

public class UpdateDeliveryStatusDto
{
    public string Status { get; set; } = null!;
    public string? DriverNote { get; set; }
}

public class PaymentVerificationResultDto
{
    public bool Success { get; set; }
    public string? OrderNumber { get; set; }
    public decimal? AmountVerified { get; set; }
    public string? Reference { get; set; }
    public string Message { get; set; } = null!;
}