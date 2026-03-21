using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Models.Entities;

namespace SellBotLk.Api.Data;

/// <summary>
/// EF Core DbContext for SellBot.lk.
/// Registers all 9 entities, configures indexes, unique constraints,
/// and seeds initial data (delivery zones + sample products).
///
/// Connection string is read from environment variable DB_CONNECTION_STRING.
/// Never hardcode credentials here — they must stay out of source control.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── DbSets (one per entity = one table in MySQL) ───────────────────────

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<InventoryLog> InventoryLogs => Set<InventoryLog>();
    public DbSet<DailyReport> DailyReports => Set<DailyReport>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Customers ──────────────────────────────────────────────────────
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(c => c.PhoneNumber).IsUnique();
        });

        // ── Products ───────────────────────────────────────────────────────
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.Category);
            entity.HasIndex(p => p.IsActive);
        });

        // ── Orders ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(o => o.OrderNumber).IsUnique();
            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.CreatedAt);

            entity.Property(o => o.Status)
                  .HasConversion<string>();

            entity.Property(o => o.PaymentStatus)
                  .HasConversion<string>();

            entity.HasOne(o => o.Customer)
                  .WithMany(c => c.Orders)
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── OrderItems ─────────────────────────────────────────────────────
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasOne(oi => oi.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(oi => oi.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.Product)
                  .WithMany(p => p.OrderItems)
                  .HasForeignKey(oi => oi.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Documents ──────────────────────────────────────────────────────
        modelBuilder.Entity<Document>(entity =>
        {
            entity.Property(d => d.Type)
                  .HasConversion<string>();

            entity.HasOne(d => d.Customer)
                  .WithMany(c => c.Documents)
                  .HasForeignKey(d => d.CustomerId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Conversations ──────────────────────────────────────────────────
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.Property(c => c.State)
                  .HasConversion<string>();

            entity.HasOne(c => c.Customer)
                  .WithMany(cu => cu.Conversations)
                  .HasForeignKey(c => c.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── InventoryLogs ──────────────────────────────────────────────────
        modelBuilder.Entity<InventoryLog>(entity =>
        {
            entity.Property(il => il.ChangeType)
                  .HasConversion<string>();

            entity.HasOne(il => il.Product)
                  .WithMany(p => p.InventoryLogs)
                  .HasForeignKey(il => il.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── DailyReports ───────────────────────────────────────────────────
        modelBuilder.Entity<DailyReport>(entity =>
        {
            entity.HasIndex(dr => dr.ReportDate).IsUnique();

            entity.HasOne(dr => dr.TopProduct)
                  .WithMany(p => p.DailyReports)
                  .HasForeignKey(dr => dr.TopProductId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Seed Data ──────────────────────────────────────────────────────
        SeedDeliveryZones(modelBuilder);
        SeedSampleProducts(modelBuilder);
    }

    private static void SeedDeliveryZones(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeliveryZone>().HasData(
            new DeliveryZone { Id = 1,  ZoneName = "Colombo",       DeliveryFee = 300,  EstimatedDays = 1, FreeDeliveryThreshold = 50000,  IsActive = true },
            new DeliveryZone { Id = 2,  ZoneName = "Gampaha",       DeliveryFee = 400,  EstimatedDays = 1, FreeDeliveryThreshold = 50000,  IsActive = true },
            new DeliveryZone { Id = 3,  ZoneName = "Kalutara",      DeliveryFee = 500,  EstimatedDays = 2, FreeDeliveryThreshold = 60000,  IsActive = true },
            new DeliveryZone { Id = 4,  ZoneName = "Kandy",         DeliveryFee = 800,  EstimatedDays = 2, FreeDeliveryThreshold = 70000,  IsActive = true },
            new DeliveryZone { Id = 5,  ZoneName = "Matale",        DeliveryFee = 900,  EstimatedDays = 2, FreeDeliveryThreshold = 70000,  IsActive = true },
            new DeliveryZone { Id = 6,  ZoneName = "Nuwara Eliya",  DeliveryFee = 1000, EstimatedDays = 3, FreeDeliveryThreshold = 80000,  IsActive = true },
            new DeliveryZone { Id = 7,  ZoneName = "Galle",         DeliveryFee = 900,  EstimatedDays = 2, FreeDeliveryThreshold = 70000,  IsActive = true },
            new DeliveryZone { Id = 8,  ZoneName = "Matara",        DeliveryFee = 1000, EstimatedDays = 3, FreeDeliveryThreshold = 80000,  IsActive = true },
            new DeliveryZone { Id = 9,  ZoneName = "Hambantota",    DeliveryFee = 1200, EstimatedDays = 3, FreeDeliveryThreshold = 80000,  IsActive = true },
            new DeliveryZone { Id = 10, ZoneName = "Jaffna",        DeliveryFee = 1500, EstimatedDays = 4, FreeDeliveryThreshold = 100000, IsActive = true },
            new DeliveryZone { Id = 11, ZoneName = "Kilinochchi",   DeliveryFee = 1600, EstimatedDays = 4, FreeDeliveryThreshold = 100000, IsActive = true },
            new DeliveryZone { Id = 12, ZoneName = "Mannar",        DeliveryFee = 1600, EstimatedDays = 4, FreeDeliveryThreshold = 100000, IsActive = true },
            new DeliveryZone { Id = 13, ZoneName = "Vavuniya",      DeliveryFee = 1500, EstimatedDays = 4, FreeDeliveryThreshold = 100000, IsActive = true },
            new DeliveryZone { Id = 14, ZoneName = "Trincomalee",   DeliveryFee = 1400, EstimatedDays = 3, FreeDeliveryThreshold = 90000,  IsActive = true },
            new DeliveryZone { Id = 15, ZoneName = "Batticaloa",    DeliveryFee = 1400, EstimatedDays = 3, FreeDeliveryThreshold = 90000,  IsActive = true },
            new DeliveryZone { Id = 16, ZoneName = "Ampara",        DeliveryFee = 1300, EstimatedDays = 3, FreeDeliveryThreshold = 90000,  IsActive = true },
            new DeliveryZone { Id = 17, ZoneName = "Ratnapura",     DeliveryFee = 800,  EstimatedDays = 2, FreeDeliveryThreshold = 70000,  IsActive = true },
            new DeliveryZone { Id = 18, ZoneName = "Kegalle",       DeliveryFee = 700,  EstimatedDays = 2, FreeDeliveryThreshold = 70000,  IsActive = true },
            new DeliveryZone { Id = 19, ZoneName = "Kurunegala",    DeliveryFee = 700,  EstimatedDays = 2, FreeDeliveryThreshold = 70000,  IsActive = true },
            new DeliveryZone { Id = 20, ZoneName = "Puttalam",      DeliveryFee = 800,  EstimatedDays = 2, FreeDeliveryThreshold = 70000,  IsActive = true },
            new DeliveryZone { Id = 21, ZoneName = "Anuradhapura",  DeliveryFee = 1100, EstimatedDays = 3, FreeDeliveryThreshold = 80000,  IsActive = true },
            new DeliveryZone { Id = 22, ZoneName = "Polonnaruwa",   DeliveryFee = 1100, EstimatedDays = 3, FreeDeliveryThreshold = 80000,  IsActive = true },
            new DeliveryZone { Id = 23, ZoneName = "Badulla",       DeliveryFee = 1000, EstimatedDays = 3, FreeDeliveryThreshold = 80000,  IsActive = true },
            new DeliveryZone { Id = 24, ZoneName = "Monaragala",    DeliveryFee = 1100, EstimatedDays = 3, FreeDeliveryThreshold = 80000,  IsActive = true },
            new DeliveryZone { Id = 25, ZoneName = "Mullaitivu",    DeliveryFee = 1700, EstimatedDays = 5, FreeDeliveryThreshold = 100000, IsActive = true }
        );
    }

    private static void SeedSampleProducts(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Product>().HasData(
            new Product
            {
                Id = 1, Name = "Executive High-Back Leather Chair",
                Category = "Chair", Price = 28500, MinPrice = 22000,
                BulkDiscountPercent = 10, BulkMinQty = 5,
                StockQty = 20, LowStockThreshold = 5,
                Color = "Black", Material = "Leather", Style = "Modern",
                Description = "Ergonomic executive chair with lumbar support, armrests, and height adjustment.",
                IsActive = true, CreatedAt = now
            },
            new Product
            {
                Id = 2, Name = "Solid Teak Dining Table (6-seater)",
                Category = "Table", Price = 75000, MinPrice = 60000,
                BulkDiscountPercent = 8, BulkMinQty = 3,
                StockQty = 8, LowStockThreshold = 2,
                Color = "Natural Wood", Material = "Solid Teak", Style = "Traditional",
                Description = "Hand-crafted solid teak dining table, seats 6 comfortably. Includes protective lacquer finish.",
                IsActive = true, CreatedAt = now
            },
            new Product
            {
                Id = 3, Name = "Three-Seater Fabric Sofa",
                Category = "Sofa", Price = 55000, MinPrice = 45000,
                BulkDiscountPercent = 5, BulkMinQty = 2,
                StockQty = 12, LowStockThreshold = 3,
                Color = "Grey", Material = "Fabric", Style = "Scandinavian",
                Description = "Comfortable 3-seater sofa with removable cushion covers. Available in grey and beige.",
                IsActive = true, CreatedAt = now
            },
            new Product
            {
                Id = 4, Name = "Steel Frame Office Desk",
                Category = "Desk", Price = 32000, MinPrice = 26000,
                BulkDiscountPercent = 12, BulkMinQty = 4,
                StockQty = 15, LowStockThreshold = 4,
                Color = "White", Material = "MDF + Steel", Style = "Modern",
                Description = "Spacious office desk with cable management and storage shelf. 140cm wide.",
                IsActive = true, CreatedAt = now
            },
            new Product
            {
                Id = 5, Name = "Wooden Bookshelf (5-tier)",
                Category = "Storage", Price = 18500, MinPrice = 14000,
                BulkDiscountPercent = 10, BulkMinQty = 5,
                StockQty = 25, LowStockThreshold = 6,
                Color = "Walnut", Material = "Engineered Wood", Style = "Industrial",
                Description = "5-tier open bookshelf with metal side supports. Easy self-assembly.",
                IsActive = true, CreatedAt = now
            }
        );
    }
}