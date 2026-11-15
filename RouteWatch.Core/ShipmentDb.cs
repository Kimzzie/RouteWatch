using Microsoft.EntityFrameworkCore;

namespace RouteWatch.Core;

public sealed class ShipmentDb(DbContextOptions<ShipmentDb> options) : DbContext(options)
{
    public DbSet<Shipment> Shipments => Set<Shipment>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Shipment>().HasIndex(s => new { s.BranchCode, s.TrackingNumber });
        builder.Entity<Shipment>().Property(s => s.BranchCode).IsRequired();
        builder.Entity<Shipment>().Property(s => s.TrackingNumber).IsRequired();
    }
}
