using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SellBotLk.Api.Models.DTOs;

namespace SellBotLk.Api.Services;

public class InvoiceService
{
    public byte[] GenerateInvoicePdf(OrderResponseDto order, string? paymentReference)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeHeader(c, order));
                page.Content().Element(c => ComposeContent(c, order, paymentReference));
                page.Footer().Element(ComposeFooter);
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private static void ComposeHeader(IContainer container, OrderResponseDto order)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("SellBot.lk")
                        .Bold().FontSize(24).FontColor(Colors.Blue.Darken2);
                    c.Item().Text("Sri Lankan Furniture & Home Goods")
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(150).AlignRight().Column(c =>
                {
                    c.Item().Text("INVOICE")
                        .Bold().FontSize(18).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"#{order.OrderNumber}")
                        .FontSize(12).FontColor(Colors.Grey.Darken1);
                });
            });

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Bill To:").SemiBold();
                    c.Item().Text(order.CustomerName);
                    c.Item().Text(order.CustomerPhone);
                    if (!string.IsNullOrEmpty(order.DeliveryAddress))
                        c.Item().Text(order.DeliveryAddress);
                });

                row.ConstantItem(150).AlignRight().Column(c =>
                {
                    c.Item().Text($"Date: {order.CreatedAt:dd MMM yyyy}");
                    c.Item().Text($"Status: {order.Status}");
                });
            });

            column.Item().PaddingTop(10);
        });
    }

    private static void ComposeContent(
        IContainer container, OrderResponseDto order, string? paymentReference)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("#");
                    header.Cell().Element(HeaderCellStyle).Text("Product");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Unit Price");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Qty");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total");

                    static IContainer HeaderCellStyle(IContainer c) =>
                        c.DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White))
                         .Background(Colors.Blue.Darken2)
                         .Padding(5);
                });

                for (var i = 0; i < order.Items.Count; i++)
                {
                    var item = order.Items[i];
                    var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                    table.Cell().Element(c => CellStyle(c, bg)).Text($"{i + 1}");
                    table.Cell().Element(c => CellStyle(c, bg)).Text(item.ProductName);
                    table.Cell().Element(c => CellStyle(c, bg)).AlignRight()
                        .Text($"LKR {item.EffectiveUnitPrice:N0}");
                    table.Cell().Element(c => CellStyle(c, bg)).AlignRight()
                        .Text($"{item.Quantity}");
                    table.Cell().Element(c => CellStyle(c, bg)).AlignRight()
                        .Text($"LKR {item.LineTotal:N0}");
                }

                static IContainer CellStyle(IContainer c, string backgroundColor) =>
                    c.Background(backgroundColor).Padding(5);
            });

            column.Item().PaddingTop(10).AlignRight().Column(totals =>
            {
                if (order.DiscountAmount > 0)
                {
                    totals.Item().Row(row =>
                    {
                        row.RelativeItem().AlignRight().Text("Discount:").SemiBold();
                        row.ConstantItem(120).AlignRight()
                            .Text($"-LKR {order.DiscountAmount:N0}");
                    });
                }

                totals.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().AlignRight().Text("Total:").Bold().FontSize(12);
                    row.ConstantItem(120).AlignRight()
                        .Text($"LKR {order.TotalAmount:N0}").Bold().FontSize(12);
                });
            });

            column.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

            column.Item().PaddingTop(10).Column(payment =>
            {
                payment.Item().Text("Payment Information").SemiBold().FontSize(12);
                payment.Item().PaddingTop(5).Text($"Status: Pending Admin Review")
                    .FontColor(Colors.Orange.Darken2);

                if (!string.IsNullOrEmpty(paymentReference))
                    payment.Item().Text($"Reference: {paymentReference}");

                payment.Item().PaddingTop(5)
                    .Text("This invoice is auto-generated after payment slip submission. " +
                          "Final confirmation is subject to admin review.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            column.Item().PaddingTop(5).AlignCenter().Text(text =>
            {
                text.Span("SellBot.lk — WhatsApp Commerce Platform | ")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }
}
