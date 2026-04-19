using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BUS_Agency_backstage.Models;

public partial class BusBookingDbContext : DbContext
{
    public BusBookingDbContext()
    {
    }

    public BusBookingDbContext(DbContextOptions<BusBookingDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<DispatchCenter> DispatchCenters { get; set; }

    public virtual DbSet<Driver> Drivers { get; set; }

    public virtual DbSet<PassengerProfile> PassengerProfiles { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Account__349DA586AB5137B5");

            entity.ToTable("Account");

            entity.HasIndex(e => e.Username, "UQ__Account__536C85E4B9CCCC57").IsUnique();

            entity.Property(e => e.AccountId)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("AccountID");
            entity.Property(e => e.CenterId).HasColumnName("CenterID");
            entity.Property(e => e.IsLocked).HasDefaultValue(false);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.Center).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.CenterId)
                .HasConstraintName("FK__Account__CenterI__2B3F6F97");

            entity.HasOne(d => d.Role).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__Account__RoleID__2A4B4B5E");
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PK__Bookings__73951ACD71B693EC");

            entity.Property(e => e.BookingId).HasColumnName("BookingID");
            entity.Property(e => e.BookingStatus).HasDefaultValue(0);
            entity.Property(e => e.CompanionCount).HasDefaultValue(0);
            entity.Property(e => e.DropoffAddr).HasMaxLength(255);
            entity.Property(e => e.PassengerId).HasColumnName("PassengerID");
            entity.Property(e => e.PickupAddr).HasMaxLength(255);
            entity.Property(e => e.PickupTime).HasColumnType("datetime");

            entity.HasOne(d => d.Passenger).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.PassengerId)
                .HasConstraintName("FK__Bookings__Passen__3A81B327");
        });

        modelBuilder.Entity<DispatchCenter>(entity =>
        {
            entity.HasKey(e => e.CenterId).HasName("PK__Dispatch__398FC7D7534CE5BF");

            entity.ToTable("DispatchCenter");

            entity.Property(e => e.CenterId)
                .ValueGeneratedNever()
                .HasColumnName("CenterID");
            entity.Property(e => e.CenterName).HasMaxLength(100);
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.DriverId).HasName("PK__Driver__F1B1CD24B2304857");

            entity.ToTable("Driver");

            entity.Property(e => e.DriverId).HasColumnName("DriverID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.DriverName).HasMaxLength(50);
            entity.Property(e => e.Mobile)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Account).WithMany(p => p.Drivers)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK__Driver__AccountI__37A5467C");
        });

        modelBuilder.Entity<PassengerProfile>(entity =>
        {
            entity.HasKey(e => e.PassengerId).HasName("PK__Passenge__88915F907BE85432");

            entity.ToTable("PassengerProfile");

            entity.Property(e => e.PassengerId)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("PassengerID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.AuditStatus).HasDefaultValue(0);
            entity.Property(e => e.DisabilityLevel).HasMaxLength(20);
            entity.Property(e => e.IdentityNo).HasMaxLength(50);
            entity.Property(e => e.RealName).HasMaxLength(50);

            entity.HasOne(d => d.Account).WithMany(p => p.PassengerProfiles)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK__Passenger__Accou__300424B4");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE3A8D60AF3C");

            entity.Property(e => e.RoleId)
                .ValueGeneratedNever()
                .HasColumnName("RoleID");
            entity.Property(e => e.RoleName).HasMaxLength(20);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.VehicleId).HasName("PK__Vehicle__476B54B221FC3B7A");

            entity.ToTable("Vehicle");

            entity.HasIndex(e => e.PlateNo, "UQ__Vehicle__48227C0C467A6D10").IsUnique();

            entity.Property(e => e.VehicleId).HasColumnName("VehicleID");
            entity.Property(e => e.PlateNo)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.Status).HasDefaultValue(0);
            entity.Property(e => e.VehicleType).HasMaxLength(20);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
