using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SellBotLk.Api.Controllers;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;
using SellBotLk.Api.Services;

namespace SellBotLk.Tests;

/// <summary>Canned Gemini replies, shaped like the real parse and vision responses.</summary>
internal static class Ai
{
    public static string OrderIntent(string productName, int quantity, decimal? offeredPrice = null) =>
        JsonSerializer.Serialize(new
        {
            intent = "Order",
            language = "en",
            orderItems = new[] { new { productName, quantity, offeredPrice } },
            replyMessage = "AI reply",
            confidence = 0.95
        });

    public static string OrderStatusIntent(string? orderNumber) =>
        JsonSerializer.Serialize(new
        {
            intent = "OrderStatus",
            language = "en",
            orderNumber,
            replyMessage = "AI reply",
            confidence = 0.95
        });

    public static string Slip(decimal amount, string? reference, double confidence = 0.99) =>
        JsonSerializer.Serialize(new { bank = "BOC", amount, reference, confidence });

    public static string Invoice(string productName, int quantity) =>
        JsonSerializer.Serialize(new
        {
            vendor = "Supplier",
            items = new[] { new { productName, quantity, unitPrice = 1, lineTotal = 1 } },
            total = 1
        });

    public static byte[] RandomImage() => Guid.NewGuid().ToByteArray();
}

[Collection("app")]
public class MinimumPriceTests
{
    private readonly TestApp _app;

    public MinimumPriceTests(TestApp app) { _app = app; _app.Upstream.Reset(); }

    [Fact]
    public async Task NegotiatedPrice_BelowFloor_IsRejected_AndLeavesStockAlone()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: 1000, minPrice: 800, stock: 5);

        var act = () => _app.CreateOrderAsync(customer.Id, product.Id, price: 799.99m);

        await act.Should().ThrowAsync<Exception>().WithMessage("*not allowed*");
        (await _app.StockOfAsync(product.Id)).Should().Be(5);
        (await _app.OrdersForAsync(customer.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task NegotiatedPrice_AtFloor_IsAccepted()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: 1000, minPrice: 800);

        var order = await _app.CreateOrderAsync(customer.Id, product.Id, quantity: 2, price: 800);

        order.TotalAmount.Should().Be(1600);
    }

    [Theory]
    [InlineData(1000.01)]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task NegotiatedPrice_AboveListOrNotPositive_IsRejected(double price)
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: 1000, minPrice: 0);

        var act = () => _app.CreateOrderAsync(customer.Id, product.Id, price: (decimal)price);

        await act.Should().ThrowAsync<Exception>().WithMessage("*not allowed*");
    }

    [Fact]
    public async Task BulkDiscount_CannotTakePriceBelowFloor()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(
            price: 1000, minPrice: 950, bulkPercent: 50, bulkMinQty: 2);

        var order = await _app.CreateOrderAsync(customer.Id, product.Id, quantity: 2);

        order.TotalAmount.Should().Be(1900);
        order.DiscountAmount.Should().Be(100);
    }

    [Fact]
    public async Task AdminApi_BelowFloor_Returns400()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: 1000, minPrice: 800);

        var response = await _app.AdminClient().PostAsJsonAsync("/api/v1/orders", new
        {
            customerId = customer.Id,
            items = new[] { new { productId = product.Id, quantity = 1, negotiatedPrice = 1m } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WhatsApp_AiPriceBelowFloor_CreatesNoOrder()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: 1000, minPrice: 800, stock: 5);
        _app.Upstream.Gemini = _ => Ai.OrderIntent(product.Name, 2, offeredPrice: 1);

        await _app.SendTextAsync(customer.PhoneNumber);

        (await _app.OrdersForAsync(customer.Id)).Should().BeEmpty();
        (await _app.StockOfAsync(product.Id)).Should().Be(5);
        _app.Upstream.TextsTo(customer.PhoneNumber).Should().ContainSingle()
            .Which.Should().Contain("unable to go lower");
    }

    [Fact]
    public async Task WhatsApp_AiPriceAboveFloor_IsDecidedByNegotiationRules()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: 1000, minPrice: 800);
        _app.Upstream.Gemini = _ => Ai.OrderIntent(product.Name, 1, offeredPrice: 900);

        await _app.SendTextAsync(customer.PhoneNumber);

        var order = (await _app.OrdersForAsync(customer.Id)).Should().ContainSingle().Subject;
        order.Items.Single().NegotiatedPrice.Should().Be(900);
    }

    [Fact]
    public async Task WhatsApp_AiPriceAboveList_ChargesListPrice()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: 1000, minPrice: 800);
        _app.Upstream.Gemini = _ => Ai.OrderIntent(product.Name, 1, offeredPrice: 999999);

        await _app.SendTextAsync(customer.PhoneNumber);

        (await _app.OrdersForAsync(customer.Id)).Single().TotalAmount.Should().Be(1000);
    }
}

[Collection("app")]
public class InvalidQuantityTests
{
    private readonly TestApp _app;

    public InvalidQuantityTests(TestApp app) { _app = app; _app.Upstream.Reset(); }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task CreateOrder_RejectsNonPositiveQuantity_AndStockIsUnchanged(int quantity)
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 5);

        var act = () => _app.CreateOrderAsync(customer.Id, product.Id, quantity);

        await act.Should().ThrowAsync<Exception>().WithMessage("*at least 1*");
        (await _app.StockOfAsync(product.Id)).Should().Be(5);
    }

    [Fact]
    public async Task CreateOrder_RejectsEmptyOrder()
    {
        var customer = await _app.NewCustomerAsync();

        var act = () => _app.WithServiceAsync<OrderService, OrderResponseDto>(orders =>
            orders.CreateOrderAsync(new CreateOrderDto { CustomerId = customer.Id }));

        await act.Should().ThrowAsync<Exception>().WithMessage("*at least one item*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task WhatsApp_AiQuantityInvalid_AsksAgain_AndCreatesNothing(int quantity)
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 5);
        _app.Upstream.Gemini = _ => Ai.OrderIntent(product.Name, quantity);

        await _app.SendTextAsync(customer.PhoneNumber);

        (await _app.OrdersForAsync(customer.Id)).Should().BeEmpty();
        (await _app.StockOfAsync(product.Id)).Should().Be(5);
        _app.Upstream.TextsTo(customer.PhoneNumber).Single().Should().Contain("how many");
    }
}

[Collection("app")]
public class StockConcurrencyTests
{
    private readonly TestApp _app;

    public StockConcurrencyTests(TestApp app) { _app = app; _app.Upstream.Reset(); }

    [Fact]
    public async Task ConcurrentOrders_CannotOversell()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 5);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
        {
            try { return await _app.CreateOrderAsync(customer.Id, product.Id); }
            catch (Exception ex)
            {
                ex.Message.Should().Contain("Insufficient stock");
                return null;
            }
        }));

        var placed = attempts.Where(o => o != null).ToList();
        placed.Should().HaveCount(5);
        placed.Select(o => o!.OrderNumber).Should().OnlyHaveUniqueItems();
        (await _app.StockOfAsync(product.Id)).Should().Be(0);
        (await _app.OrdersForAsync(customer.Id)).Should().HaveCount(5);
    }

    [Fact]
    public async Task ConcurrentMultiItemOrders_InOppositeOrder_DoNotDeadlockOrLoseStock()
    {
        var customer = await _app.NewCustomerAsync();
        var a = await _app.NewProductAsync(stock: 100);
        var b = await _app.NewProductAsync(stock: 100);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
        {
            var (first, second) = i % 2 == 0 ? (a.Id, b.Id) : (b.Id, a.Id);
            return _app.WithServiceAsync<OrderService, OrderResponseDto>(orders =>
                orders.CreateOrderAsync(new CreateOrderDto
                {
                    CustomerId = customer.Id,
                    Items = new() { new() { ProductId = first, Quantity = 1 }, new() { ProductId = second, Quantity = 1 } }
                }));
        }));

        (await _app.StockOfAsync(a.Id)).Should().Be(80);
        (await _app.StockOfAsync(b.Id)).Should().Be(80);
    }

    [Fact]
    public async Task FailedItem_RollsBackEarlierItems_AndServiceRemainsUsable()
    {
        var customer = await _app.NewCustomerAsync();
        var plenty = await _app.NewProductAsync(stock: 10);
        var scarce = await _app.NewProductAsync(stock: 1);

        // The same service instance runs both, as it does for a batch of webhook messages.
        await _app.WithServiceAsync<OrderService, bool>(async orders =>
        {
            var act = () => orders.CreateOrderAsync(new CreateOrderDto
            {
                CustomerId = customer.Id,
                Items = new()
                {
                    new() { ProductId = plenty.Id, Quantity = 3 },
                    new() { ProductId = scarce.Id, Quantity = 2 }
                }
            });
            await act.Should().ThrowAsync<Exception>().WithMessage("*Insufficient stock*");

            await orders.CreateOrderAsync(new CreateOrderDto
            {
                CustomerId = customer.Id,
                Items = new() { new() { ProductId = plenty.Id, Quantity = 1 } }
            });
            return true;
        });

        (await _app.StockOfAsync(plenty.Id)).Should().Be(9);
        (await _app.StockOfAsync(scarce.Id)).Should().Be(1);
        (await _app.OrdersForAsync(customer.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task RepeatedProductLines_AreCheckedTogether()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 3);

        var act = () => _app.WithServiceAsync<OrderService, OrderResponseDto>(orders =>
            orders.CreateOrderAsync(new CreateOrderDto
            {
                CustomerId = customer.Id,
                Items = new()
                {
                    new() { ProductId = product.Id, Quantity = 2 },
                    new() { ProductId = product.Id, Quantity = 2 }
                }
            }));

        await act.Should().ThrowAsync<Exception>().WithMessage("*Insufficient stock*");
        (await _app.StockOfAsync(product.Id)).Should().Be(3);
    }

    [Fact]
    public async Task CreatedOrder_HasRealNumber_ProductNames_AndAuditLog()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 10);

        var order = await _app.CreateOrderAsync(customer.Id, product.Id, quantity: 4);

        Regex.IsMatch(order.OrderNumber, @"^ORD-\d{4}-\d{3,}$").Should().BeTrue(order.OrderNumber);
        order.Items.Single().ProductName.Should().Be(product.Name);

        var log = await _app.WithDbAsync(db => db.InventoryLogs.AsNoTracking()
            .SingleAsync(l => l.ProductId == product.Id));
        log.ChangeType.Should().Be(InventoryChangeType.OrderDeduction);
        log.QuantityBefore.Should().Be(10);
        log.QuantityAfter.Should().Be(6);
        log.ReferenceId.Should().Be(order.OrderNumber);
    }
}

[Collection("app")]
public class WebhookDeliveryTests
{
    private readonly TestApp _app;

    public WebhookDeliveryTests(TestApp app) { _app = app; _app.Upstream.Reset(); }

    private async Task<(Customer Customer, Product Product, object Body)> OrderMessageAsync()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 10);
        _app.Upstream.Gemini = _ => Ai.OrderIntent(product.Name, 1);
        return (customer, product, new { body = $"order {Guid.NewGuid():N}" });
    }

    [Fact]
    public async Task SameMessageId_DeliveredTwice_CreatesOneOrder()
    {
        var (customer, product, body) = await OrderMessageAsync();
        var messageId = TestApp.NewMessageId();

        await _app.PostAsync(customer.PhoneNumber, messageId, "text", body);
        await _app.PostAsync(customer.PhoneNumber, messageId, "text", body);

        (await _app.OrdersForAsync(customer.Id)).Should().ContainSingle();
        (await _app.StockOfAsync(product.Id)).Should().Be(9);
    }

    [Fact]
    public async Task SameMessageId_DeliveredConcurrently_CreatesOneOrder()
    {
        var (customer, product, body) = await OrderMessageAsync();
        var messageId = TestApp.NewMessageId();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            _app.PostAsync(customer.PhoneNumber, messageId, "text", body)));

        (await _app.OrdersForAsync(customer.Id)).Should().ContainSingle();
        (await _app.StockOfAsync(product.Id)).Should().Be(9);
    }

    [Fact]
    public async Task DifferentMessageIds_AreBothProcessed()
    {
        var (customer, _, body) = await OrderMessageAsync();

        await _app.PostAsync(customer.PhoneNumber, TestApp.NewMessageId(), "text", body);
        await _app.PostAsync(customer.PhoneNumber, TestApp.NewMessageId(), "text", body);

        (await _app.OrdersForAsync(customer.Id)).Should().HaveCount(2);
    }

    [Fact]
    public async Task SenderOverLimit_IsDropped_WithoutAffectingOtherSenders()
    {
        var noisy = await _app.NewCustomerAsync();
        var other = await _app.NewCustomerAsync();

        // "sticker" is an unsupported type: one reply per processed message, no AI involved.
        for (var i = 0; i < 25; i++)
            await _app.PostAsync(noisy.PhoneNumber, TestApp.NewMessageId(), "sticker", new { id = "s" });
        await _app.PostAsync(other.PhoneNumber, TestApp.NewMessageId(), "sticker", new { id = "s" });

        _app.Upstream.TextsTo(noisy.PhoneNumber).Should().HaveCount(20);
        _app.Upstream.TextsTo(other.PhoneNumber).Should().ContainSingle();
    }

    [Fact]
    public async Task BadSignature_IsRejected_AndDoesNothing()
    {
        var (customer, _, body) = await OrderMessageAsync();
        var json = TestApp.Payload(customer.PhoneNumber, TestApp.NewMessageId(), "text", body);

        var response = await _app.CreateClient().SendAsync(TestApp.SignedWebhook(json, "wrong-secret"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _app.OrdersForAsync(customer.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task UnsignedFlood_CannotExhaustWebhookRateLimit()
    {
        using var isolated = _app.WithWebHostBuilder(_ => { });
        var client = isolated.CreateClient();

        for (var i = 0; i < 650; i++)
        {
            var response = await client.PostAsync("/api/v1/webhook/whatsapp", new StringContent("{}"));
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var customer = await _app.NewCustomerAsync();
        var signed = TestApp.SignedWebhook(
            TestApp.Payload(customer.PhoneNumber, TestApp.NewMessageId(), "sticker", new { id = "s" }));

        (await client.SendAsync(signed)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[Collection("app")]
public class PaymentReviewTests
{
    private readonly TestApp _app;

    public PaymentReviewTests(TestApp app) { _app = app; _app.Upstream.Reset(); }

    private async Task<(Customer Customer, OrderResponseDto Order)> UnpaidOrderAsync(decimal price = 1000)
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: price, minPrice: price / 2);
        return (customer, await _app.CreateOrderAsync(customer.Id, product.Id));
    }

    private static string Reference() => $"TXN{Guid.NewGuid():N}";

    private Task<HttpResponseMessage> AdminPutAsync(string path) =>
        _app.AdminClient().PutAsJsonAsync(path, new { });

    [Fact]
    public async Task Slip_GoesToPendingReview_NeverPaid_AndSendsNoInvoice()
    {
        var (customer, order) = await UnpaidOrderAsync();
        var reference = Reference();
        _app.Upstream.Gemini = _ => Ai.Slip(1000, reference);

        await _app.SendMediaAsync(customer.PhoneNumber, "image", Ai.RandomImage());

        var saved = await _app.OrderAsync(order.Id);
        saved.PaymentStatus.Should().Be(PaymentStatus.PendingReview);
        saved.PaymentReference.Should().Be(reference.ToUpperInvariant());
        saved.Status.Should().Be(OrderStatus.Confirmed);
        _app.Upstream.DocumentsTo(customer.PhoneNumber).Should().Be(0);
        _app.Upstream.TextsTo(customer.PhoneNumber).Last().Should().Contain("will verify");
        _app.Upstream.TextsTo(TestApp.OwnerPhone).Last().Should().Contain("needs review");
    }

    [Fact]
    public async Task AdminUploadedSlip_AlsoGoesToPendingReview()
    {
        var (customer, order) = await UnpaidOrderAsync();
        _app.Upstream.Gemini = _ => Ai.Slip(1000, Reference());

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Ai.RandomImage());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "slip.png");
        form.Add(new StringContent("PaymentSlip"), "DocumentType");
        form.Add(new StringContent(customer.Id.ToString()), "CustomerId");

        var response = await _app.AdminClient().PostAsync("/api/v1/documents/process", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _app.OrderAsync(order.Id)).PaymentStatus.Should().Be(PaymentStatus.PendingReview);
    }

    [Fact]
    public async Task Approve_MarksPaid_SendsInvoice_AndKeepsFulfilmentStatus()
    {
        var (customer, order) = await UnpaidOrderAsync();
        _app.Upstream.Gemini = _ => Ai.Slip(1000, Reference());
        await _app.SendMediaAsync(customer.PhoneNumber, "image", Ai.RandomImage());
        await _app.AdminClient().PutAsJsonAsync($"/api/v1/orders/{order.Id}/status", new { status = "Processing" });

        var response = await AdminPutAsync($"/api/v1/orders/{order.Id}/payment/approve");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await _app.OrderAsync(order.Id);
        saved.PaymentStatus.Should().Be(PaymentStatus.Paid);
        saved.Status.Should().Be(OrderStatus.Processing);
        _app.Upstream.DocumentsTo(customer.PhoneNumber).Should().Be(1);
    }

    [Fact]
    public async Task Approve_WithoutSlip_OrTwice_IsRefused()
    {
        var (customer, order) = await UnpaidOrderAsync();

        (await AdminPutAsync($"/api/v1/orders/{order.Id}/payment/approve"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _app.OrderAsync(order.Id)).PaymentStatus.Should().Be(PaymentStatus.Unpaid);

        _app.Upstream.Gemini = _ => Ai.Slip(1000, Reference());
        await _app.SendMediaAsync(customer.PhoneNumber, "image", Ai.RandomImage());
        (await AdminPutAsync($"/api/v1/orders/{order.Id}/payment/approve"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await AdminPutAsync($"/api/v1/orders/{order.Id}/payment/approve"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task PaymentDecisions_RequireAdminLogin(string action)
    {
        var (_, order) = await UnpaidOrderAsync();

        var response = await _app.CreateClient()
            .PutAsJsonAsync($"/api/v1/orders/{order.Id}/payment/{action}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Approve_OnCancelledOrder_IsRefused()
    {
        var (customer, order) = await UnpaidOrderAsync();
        _app.Upstream.Gemini = _ => Ai.Slip(1000, Reference());
        await _app.SendMediaAsync(customer.PhoneNumber, "image", Ai.RandomImage());
        await AdminPutAsync($"/api/v1/orders/{order.Id}/cancel");

        (await AdminPutAsync($"/api/v1/orders/{order.Id}/payment/approve"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reject_ReturnsToUnpaid_NotifiesCustomer_AndAllowsResubmission()
    {
        var (customer, order) = await UnpaidOrderAsync();
        var image = Ai.RandomImage();
        var reference = Reference();
        _app.Upstream.Gemini = _ => Ai.Slip(1000, reference);
        await _app.SendMediaAsync(customer.PhoneNumber, "image", image);

        var response = await _app.AdminClient().PutAsJsonAsync(
            $"/api/v1/orders/{order.Id}/payment/reject", new { reason = "Transfer not received" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await _app.OrderAsync(order.Id);
        saved.PaymentStatus.Should().Be(PaymentStatus.Unpaid);
        saved.PaymentReference.Should().BeNull();
        _app.Upstream.TextsTo(customer.PhoneNumber).Last().Should().Contain("Transfer not received");

        await _app.SendMediaAsync(customer.PhoneNumber, "image", image);
        (await _app.OrderAsync(order.Id)).PaymentStatus.Should().Be(PaymentStatus.PendingReview);
    }

    [Fact]
    public async Task SlipImage_IsAvailableToAdminOnly()
    {
        var (customer, order) = await UnpaidOrderAsync();
        var image = Ai.RandomImage();
        _app.Upstream.Gemini = _ => Ai.Slip(1000, Reference());
        await _app.SendMediaAsync(customer.PhoneNumber, "image", image);

        var response = await _app.AdminClient().GetAsync($"/api/v1/orders/{order.Id}/payment-slip");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(image);
        (await _app.CreateClient().GetAsync($"/api/v1/orders/{order.Id}/payment-slip"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SameImage_CannotPayASecondOrder_EvenWithNoReference()
    {
        var (first, firstOrder) = await UnpaidOrderAsync();
        var (second, secondOrder) = await UnpaidOrderAsync();
        var image = Ai.RandomImage();
        _app.Upstream.Gemini = _ => Ai.Slip(1000, null);

        await _app.SendMediaAsync(first.PhoneNumber, "image", image);
        await _app.SendMediaAsync(second.PhoneNumber, "image", image);

        (await _app.OrderAsync(firstOrder.Id)).PaymentStatus.Should().Be(PaymentStatus.PendingReview);
        (await _app.OrderAsync(secondOrder.Id)).PaymentStatus.Should().Be(PaymentStatus.Unpaid);
        _app.Upstream.TextsTo(second.PhoneNumber).Last().Should().Contain("already been submitted");
        _app.Upstream.TextsTo(TestApp.OwnerPhone).Last().Should().Contain("Reused payment slip");
    }

    [Theory]
    [InlineData("txn-555-{0}", "TXN 555 {0}")]
    [InlineData("REF/{0}", "ref {0}")]
    public async Task SameReference_DifferentImage_CannotPayASecondOrder(string firstForm, string secondForm)
    {
        var (first, firstOrder) = await UnpaidOrderAsync();
        var (second, secondOrder) = await UnpaidOrderAsync();
        var id = Guid.NewGuid().ToString("N");

        _app.Upstream.Gemini = _ => Ai.Slip(1000, string.Format(firstForm, id));
        await _app.SendMediaAsync(first.PhoneNumber, "image", Ai.RandomImage());

        _app.Upstream.Gemini = _ => Ai.Slip(1000, string.Format(secondForm, id));
        await _app.SendMediaAsync(second.PhoneNumber, "image", Ai.RandomImage());

        (await _app.OrderAsync(firstOrder.Id)).PaymentStatus.Should().Be(PaymentStatus.PendingReview);
        (await _app.OrderAsync(secondOrder.Id)).PaymentStatus.Should().Be(PaymentStatus.Unpaid);
    }

    [Fact]
    public async Task ApprovedSlip_RemainsClaimed_ForLaterOrders()
    {
        var (customer, firstOrder) = await UnpaidOrderAsync();
        var image = Ai.RandomImage();
        var reference = Reference();
        _app.Upstream.Gemini = _ => Ai.Slip(1000, reference);
        await _app.SendMediaAsync(customer.PhoneNumber, "image", image);
        await AdminPutAsync($"/api/v1/orders/{firstOrder.Id}/payment/approve");

        var product = await _app.NewProductAsync(price: 1000, minPrice: 500);
        var laterOrder = await _app.CreateOrderAsync(customer.Id, product.Id);
        await _app.SendMediaAsync(customer.PhoneNumber, "image", image);

        (await _app.OrderAsync(laterOrder.Id)).PaymentStatus.Should().Be(PaymentStatus.Unpaid);
    }

    [Fact]
    public async Task SameSlip_SubmittedConcurrently_ClaimsOnlyOneOrder()
    {
        var pairs = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => UnpaidOrderAsync()));
        var image = Ai.RandomImage();
        var reference = Reference();
        _app.Upstream.Gemini = _ => Ai.Slip(1000, reference);

        await Task.WhenAll(pairs.Select(p =>
            _app.SendMediaAsync(p.Customer.PhoneNumber, "image", image)));

        var statuses = await Task.WhenAll(pairs.Select(async p =>
            (await _app.OrderAsync(p.Order.Id)).PaymentStatus));
        statuses.Count(s => s == PaymentStatus.PendingReview).Should().Be(1);
        statuses.Count(s => s == PaymentStatus.Unpaid).Should().Be(5);
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("none")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task MissingReferences_DoNotCollideWithEachOther(string? placeholder)
    {
        var (first, firstOrder) = await UnpaidOrderAsync();
        var (second, secondOrder) = await UnpaidOrderAsync();
        _app.Upstream.Gemini = _ => Ai.Slip(1000, placeholder);

        await _app.SendMediaAsync(first.PhoneNumber, "image", Ai.RandomImage());
        await _app.SendMediaAsync(second.PhoneNumber, "image", Ai.RandomImage());

        (await _app.OrderAsync(firstOrder.Id)).PaymentReference.Should().BeNull();
        (await _app.OrderAsync(secondOrder.Id)).PaymentStatus.Should().Be(PaymentStatus.PendingReview);
    }
}

[Collection("app")]
public class SupplierInvoiceTests
{
    private readonly TestApp _app;

    public SupplierInvoiceTests(TestApp app) { _app = app; _app.Upstream.Reset(); }

    [Fact]
    public async Task CustomerDocument_WithNoOpenOrder_IsRefused_BeforeAnyProcessing()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 5);
        _app.Upstream.Gemini = _ => Ai.Invoice(product.Name, 1000);

        await _app.SendMediaAsync(customer.PhoneNumber, "document", Ai.RandomImage(), "application/pdf");

        (await _app.StockOfAsync(product.Id)).Should().Be(5);
        _app.Upstream.GeminiCalls.Should().Be(0);
        _app.Upstream.MediaDownloads.Should().Be(0);
    }

    [Fact]
    public async Task CustomerDocument_WithOpenOrder_IsTreatedAsSlip_AndCannotAddStock()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 5);
        await _app.CreateOrderAsync(customer.Id, product.Id);
        _app.Upstream.Gemini = _ => Ai.Invoice(product.Name, 1000);

        await _app.SendMediaAsync(customer.PhoneNumber, "document", Ai.RandomImage(), "application/pdf");

        (await _app.StockOfAsync(product.Id)).Should().Be(4);
        var restocks = await _app.WithDbAsync(db => db.InventoryLogs
            .CountAsync(l => l.ProductId == product.Id && l.ChangeType == InventoryChangeType.SupplierRestock));
        restocks.Should().Be(0);
    }

    [Fact]
    public async Task InvoiceService_RefusesNonOwnerPhone_EvenWhenCalledDirectly()
    {
        var product = await _app.NewProductAsync(stock: 5);
        _app.Upstream.Gemini = _ => Ai.Invoice(product.Name, 1000);

        var act = () => _app.WithServiceAsync<DocumentService, DocumentResponseDto>(documents =>
            documents.ProcessDocumentAsync(
                Ai.RandomImage(), "application/pdf", DocumentType.SupplierInvoice, null, "94779999999"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _app.StockOfAsync(product.Id)).Should().Be(5);
    }

    [Fact]
    public async Task OwnerDocument_Restocks_WithAuditLog()
    {
        var product = await _app.NewProductAsync(stock: 5);
        _app.Upstream.Gemini = _ => Ai.Invoice(product.Name, 7);

        await _app.SendMediaAsync(TestApp.OwnerPhone, "document", Ai.RandomImage(), "application/pdf");

        (await _app.StockOfAsync(product.Id)).Should().Be(12);
        var log = await _app.WithDbAsync(db => db.InventoryLogs.AsNoTracking()
            .SingleAsync(l => l.ProductId == product.Id));
        log.ChangeType.Should().Be(InventoryChangeType.SupplierRestock);
        log.QuantityBefore.Should().Be(5);
        log.QuantityAfter.Should().Be(12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task OwnerInvoice_WithBadAiQuantity_DoesNotChangeStock(int quantity)
    {
        var product = await _app.NewProductAsync(stock: 5);
        _app.Upstream.Gemini = _ => Ai.Invoice(product.Name, quantity);

        await _app.SendMediaAsync(TestApp.OwnerPhone, "document", Ai.RandomImage(), "application/pdf");

        (await _app.StockOfAsync(product.Id)).Should().Be(5);
    }

    [Fact]
    public async Task AdminApiInvoiceUpload_StillWorks()
    {
        var product = await _app.NewProductAsync(stock: 5);
        _app.Upstream.Gemini = _ => Ai.Invoice(product.Name, 3);

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Ai.RandomImage());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "invoice.png");
        form.Add(new StringContent("SupplierInvoice"), "DocumentType");

        (await _app.AdminClient().PostAsync("/api/v1/documents/process", form))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _app.StockOfAsync(product.Id)).Should().Be(8);
    }

    [Theory]
    [InlineData("94770000001", true)]
    [InlineData("+94 77 000 0001", true)]
    [InlineData("94770000002", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public async Task IsOwnerPhone_ComparesDigitsOnly(string? phone, bool expected)
    {
        (await _app.WithServiceAsync<DocumentService, bool>(d => Task.FromResult(d.IsOwnerPhone(phone))))
            .Should().Be(expected);
    }
}

[Collection("app")]
public class OrderPrivacyTests
{
    private readonly TestApp _app;

    public OrderPrivacyTests(TestApp app) { _app = app; _app.Upstream.Reset(); }

    [Fact]
    public async Task AnotherCustomersOrderNumber_RevealsNothing_AndMatchesUnknownNumber()
    {
        var owner = await _app.NewCustomerAsync();
        var stranger = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: 7654);
        var order = await _app.CreateOrderAsync(owner.Id, product.Id);

        _app.Upstream.Gemini = _ => Ai.OrderStatusIntent(order.OrderNumber);
        await _app.SendTextAsync(stranger.PhoneNumber);
        var replyForOtherOrder = _app.Upstream.TextsTo(stranger.PhoneNumber).Single();

        _app.Upstream.Sent.Clear();
        _app.Upstream.Gemini = _ => Ai.OrderStatusIntent("ORD-1999-999");
        await _app.SendTextAsync(stranger.PhoneNumber);
        var replyForUnknownOrder = _app.Upstream.TextsTo(stranger.PhoneNumber).Single();

        replyForOtherOrder.Should().NotContain(order.OrderNumber).And.NotContain("7,654");
        replyForOtherOrder.Should().Be(replyForUnknownOrder);
    }

    [Fact]
    public async Task AnotherCustomersNumber_DoesNotFallBackToYourOwnOrder()
    {
        var owner = await _app.NewCustomerAsync();
        var asker = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync();
        var theirOrder = await _app.CreateOrderAsync(owner.Id, product.Id);
        var ownOrder = await _app.CreateOrderAsync(asker.Id, product.Id);

        _app.Upstream.Gemini = _ => Ai.OrderStatusIntent(theirOrder.OrderNumber);
        await _app.SendTextAsync(asker.PhoneNumber);

        _app.Upstream.TextsTo(asker.PhoneNumber).Single().Should().NotContain(ownOrder.OrderNumber);
    }

    [Fact]
    public async Task OwnOrderNumber_ShowsStatus()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: 7654);
        var order = await _app.CreateOrderAsync(customer.Id, product.Id);

        _app.Upstream.Gemini = _ => Ai.OrderStatusIntent(order.OrderNumber);
        await _app.SendTextAsync(customer.PhoneNumber);

        var reply = _app.Upstream.TextsTo(customer.PhoneNumber).Single();
        reply.Should().Contain(order.OrderNumber).And.Contain("7,654");
    }

    [Fact]
    public async Task NoOrderNumber_ShowsOnlyYourOwnLatestOrder()
    {
        var other = await _app.NewCustomerAsync();
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync();
        var mine = await _app.CreateOrderAsync(customer.Id, product.Id);
        var newerButNotMine = await _app.CreateOrderAsync(other.Id, product.Id);

        _app.Upstream.Gemini = _ => Ai.OrderStatusIntent(null);
        await _app.SendTextAsync(customer.PhoneNumber);

        var reply = _app.Upstream.TextsTo(customer.PhoneNumber).Single();
        reply.Should().Contain(mine.OrderNumber).And.NotContain(newerButNotMine.OrderNumber);
    }

    [Fact]
    public async Task Slip_CanOnlyMatchTheSendersOwnOrders()
    {
        var owner = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(price: 1000, minPrice: 500);
        var ownersOrder = await _app.CreateOrderAsync(owner.Id, product.Id);
        var stranger = await _app.NewCustomerAsync();
        var strangersOrder = await _app.CreateOrderAsync(stranger.Id, product.Id);
        _app.Upstream.Gemini = _ => Ai.Slip(1000, $"TXN{Guid.NewGuid():N}");

        await _app.SendMediaAsync(stranger.PhoneNumber, "image", Ai.RandomImage());

        (await _app.OrderAsync(ownersOrder.Id)).PaymentStatus.Should().Be(PaymentStatus.Unpaid);
        (await _app.OrderAsync(strangersOrder.Id)).PaymentStatus.Should().Be(PaymentStatus.PendingReview);
    }
}

[Collection("app")]
public class CancellationTests
{
    private readonly TestApp _app;

    public CancellationTests(TestApp app) { _app = app; _app.Upstream.Reset(); }

    private Task BackdateAsync(int orderId, int hours) =>
        _app.WithDbAsync(db => db.Orders.Where(o => o.Id == orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.UpdatedAt, DateTime.UtcNow.AddHours(-hours))));

    private async Task<int> CancelThroughAsync(string path, int orderId)
    {
        var client = _app.AdminClient();
        switch (path)
        {
            case "cancel-endpoint":
                (await client.PutAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new { }))
                    .StatusCode.Should().Be(HttpStatusCode.OK);
                break;
            case "status-endpoint":
                (await client.PutAsJsonAsync($"/api/v1/orders/{orderId}/status", new { status = "Cancelled" }))
                    .StatusCode.Should().Be(HttpStatusCode.OK);
                break;
            case "delivery-endpoint":
                (await client.PutAsJsonAsync($"/api/v1/orders/{orderId}/delivery-status", new { status = "Cancelled" }))
                    .StatusCode.Should().Be(HttpStatusCode.OK);
                break;
            case "expiry":
                await BackdateAsync(orderId, 25);
                await _app.WithServiceAsync<OrderService, int>(o => o.ExpireUnpaidOrdersAsync());
                break;
        }
        return orderId;
    }

    [Theory]
    [InlineData("cancel-endpoint")]
    [InlineData("status-endpoint")]
    [InlineData("delivery-endpoint")]
    [InlineData("expiry")]
    public async Task EveryCancellationPath_RestoresStockOnce_WithAuditLog(string path)
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 10);
        var order = await _app.CreateOrderAsync(customer.Id, product.Id, quantity: 4);
        (await _app.StockOfAsync(product.Id)).Should().Be(6);

        await CancelThroughAsync(path, order.Id);

        (await _app.OrderAsync(order.Id)).Status.Should().Be(OrderStatus.Cancelled);
        (await _app.StockOfAsync(product.Id)).Should().Be(10);
        var log = await _app.WithDbAsync(db => db.InventoryLogs.AsNoTracking()
            .SingleAsync(l => l.ProductId == product.Id && l.ChangeType == InventoryChangeType.Return));
        log.QuantityBefore.Should().Be(6);
        log.QuantityAfter.Should().Be(10);
        log.ReferenceId.Should().Be(order.OrderNumber);
    }

    [Theory]
    [InlineData("cancel-endpoint")]
    [InlineData("status-endpoint")]
    [InlineData("delivery-endpoint")]
    public async Task CancellingTwice_IsRefused_AndDoesNotRestockAgain(string path)
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 10);
        var order = await _app.CreateOrderAsync(customer.Id, product.Id, quantity: 4);
        await CancelThroughAsync(path, order.Id);

        var second = await _app.AdminClient().PutAsJsonAsync(
            path switch
            {
                "cancel-endpoint" => $"/api/v1/orders/{order.Id}/cancel",
                "status-endpoint" => $"/api/v1/orders/{order.Id}/status",
                _ => $"/api/v1/orders/{order.Id}/delivery-status"
            },
            new { status = "Cancelled" });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _app.StockOfAsync(product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task ConcurrentCancels_RestoreStockExactlyOnce()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 10);
        var order = await _app.CreateOrderAsync(customer.Id, product.Id, quantity: 4);
        var succeeded = 0;

        // Holding the row lock lets every cancel read the order as still open and
        // queue on its update, which forces the race instead of hoping for it.
        await _app.WithDbAsync(async db =>
        {
            await using var hold = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT Id FROM Orders WHERE Id = {order.Id} FOR UPDATE");

            var cancels = Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
            {
                try
                {
                    await _app.WithServiceAsync<OrderService, OrderResponseDto?>(o => o.CancelOrderAsync(order.Id));
                    Interlocked.Increment(ref succeeded);
                }
                catch (Exception ex) { ex.Message.Should().Contain("already cancelled"); }
            }));

            await Task.Delay(1500);
            await hold.RollbackAsync();
            await cancels;
        });

        succeeded.Should().Be(1);
        (await _app.StockOfAsync(product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task DeliveredOrder_CannotBeCancelled()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 10);
        var order = await _app.CreateOrderAsync(customer.Id, product.Id);
        await _app.WithDbAsync(db => db.Orders.Where(o => o.Id == order.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Delivered)));

        (await _app.AdminClient().PutAsJsonAsync($"/api/v1/orders/{order.Id}/cancel", new { }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _app.StockOfAsync(product.Id)).Should().Be(9);
    }

    [Fact]
    public async Task DeliveryPath_NonCancelTransitions_StillWork_AndNotifyCustomer()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 10);
        var order = await _app.CreateOrderAsync(customer.Id, product.Id);

        var response = await _app.AdminClient().PutAsJsonAsync(
            $"/api/v1/orders/{order.Id}/delivery-status", new { status = "Processing", driverNote = "Fragile" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await _app.OrderAsync(order.Id);
        saved.Status.Should().Be(OrderStatus.Processing);
        saved.DriverNote.Should().Be("Fragile");
        _app.Upstream.TextsTo(customer.PhoneNumber).Single().Should().Contain("being processed");
    }

    [Fact]
    public async Task Expiry_NotifiesCustomer()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync();
        var order = await _app.CreateOrderAsync(customer.Id, product.Id);

        await CancelThroughAsync("expiry", order.Id);

        _app.Upstream.TextsTo(customer.PhoneNumber).Single().Should().Contain(order.OrderNumber);
    }

    [Fact]
    public async Task Expiry_LeavesRecentOrdersAlone()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 10);
        var order = await _app.CreateOrderAsync(customer.Id, product.Id);
        await BackdateAsync(order.Id, 23);

        await _app.WithServiceAsync<OrderService, int>(o => o.ExpireUnpaidOrdersAsync());

        (await _app.OrderAsync(order.Id)).Status.Should().Be(OrderStatus.Confirmed);
        (await _app.StockOfAsync(product.Id)).Should().Be(9);
    }

    [Theory]
    [InlineData(PaymentStatus.PendingReview, OrderStatus.Confirmed)]
    [InlineData(PaymentStatus.Paid, OrderStatus.Confirmed)]
    [InlineData(PaymentStatus.Unpaid, OrderStatus.Processing)]
    public async Task Expiry_LeavesOrdersInReviewPaidOrBeingProcessedAlone(
        PaymentStatus payment, OrderStatus status)
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 10);
        var order = await _app.CreateOrderAsync(customer.Id, product.Id);
        await _app.WithDbAsync(db => db.Orders.Where(o => o.Id == order.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.PaymentStatus, payment)
                .SetProperty(o => o.Status, status)
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow.AddHours(-100))));

        await _app.WithServiceAsync<OrderService, int>(o => o.ExpireUnpaidOrdersAsync());

        (await _app.OrderAsync(order.Id)).Status.Should().Be(status);
        (await _app.StockOfAsync(product.Id)).Should().Be(9);
    }

    [Fact]
    public async Task ExpiryThatLosesRaceToASlip_DoesNotCancel()
    {
        var customer = await _app.NewCustomerAsync();
        var product = await _app.NewProductAsync(stock: 10);
        var order = await _app.CreateOrderAsync(customer.Id, product.Id);
        await _app.WithDbAsync(db => db.Orders.Where(o => o.Id == order.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, PaymentStatus.PendingReview)));

        var result = await _app.WithServiceAsync<OrderService, OrderResponseDto?>(o =>
            o.CancelOrderAsync(order.Id, onlyIfUnpaid: true));

        result.Should().BeNull();
        (await _app.StockOfAsync(product.Id)).Should().Be(9);
    }
}

[Collection("app")]
public class AdminLoginTests
{
    private readonly TestApp _app;

    public AdminLoginTests(TestApp app) { _app = app; _app.Upstream.Reset(); }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });

    [Fact]
    public async Task CorrectCredentials_ReturnAWorkingToken()
    {
        using var host = _app.WithWebHostBuilder(_ => { });
        var client = host.CreateClient();

        var response = await LoginAsync(client, "owner", TestApp.AdminPassword);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.GetAsync("/api/v1/orders")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("admin", "admin")]
    [InlineData("owner", "admin")]
    [InlineData("admin", TestApp.AdminPassword)]
    [InlineData("owner", "wrong-password-123")]
    public async Task WrongOrDefaultCredentials_AreRejected(string username, string password)
    {
        using var host = _app.WithWebHostBuilder(_ => { });

        (await LoginAsync(host.CreateClient(), username, password))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("admin", "")]
    [InlineData("admin", "short")]
    public void UnconfiguredAdmin_NoCredentialsWork_NotEvenAdminAdmin(string? username, string? password)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Username"] = username,
                ["Admin:Password"] = password,
                ["Jwt:Secret"] = "test-jwt-secret-test-jwt-secret-test-jwt-secret"
            })
            .Build();
        var controller = new AuthController(config, NullLogger<AuthController>.Instance);

        var result = controller.Login(new LoginRequest { Username = "admin", Password = "admin" });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("            ", false)]
    [InlineData("admin", false)]
    [InlineData("changeme", false)]
    [InlineData("elevenchars", false)]
    [InlineData("twelve-chars", true)]
    public void PasswordPolicy(string? password, bool expected)
    {
        AuthController.IsAcceptablePassword(password).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("admin")]
    public void AppRefusesToStart_WithoutAProperPassword(string password)
    {
        Environment.SetEnvironmentVariable("Admin__Password", password);
        try
        {
            using var host = _app.WithWebHostBuilder(_ => { });

            var act = () => host.CreateClient();

            act.Should().Throw<Exception>()
                .Where(e => e.ToString().Contains("Admin credentials are not configured"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Admin__Password", TestApp.AdminPassword);
        }
    }

    [Fact]
    public async Task LoginIsRateLimited_EvenForCorrectPasswordAfterTheLimit()
    {
        using var host = _app.WithWebHostBuilder(_ => { });
        var client = host.CreateClient();

        for (var i = 0; i < 5; i++)
            (await LoginAsync(client, "owner", "wrong-password-123"))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await LoginAsync(client, "owner", TestApp.AdminPassword))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AdminEndpoints_RequireAToken()
    {
        (await _app.CreateClient().GetAsync("/api/v1/orders"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
