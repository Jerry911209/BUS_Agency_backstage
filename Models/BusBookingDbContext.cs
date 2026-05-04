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

    public virtual DbSet<Announcement> Announcements { get; set; }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<DispatchCenter> DispatchCenters { get; set; }

    public virtual DbSet<DispatchTask> DispatchTasks { get; set; }

    public virtual DbSet<Driver> Drivers { get; set; }

    public virtual DbSet<DriverCheckLog> DriverCheckLogs { get; set; }

    public virtual DbSet<DrivingBehavior> DrivingBehaviors { get; set; }

    public virtual DbSet<Feedback> Feedbacks { get; set; }

    public virtual DbSet<FuelLog> FuelLogs { get; set; }

    public virtual DbSet<Gpslog> Gpslogs { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<PassengerProfile> PassengerProfiles { get; set; }

    public virtual DbSet<Relationship> Relationships { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=203.64.84.56,1433;Database=BusBookingDB;User ID=tcumi;Password=tcumi;Trusted_Connection=False;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Account__349DA586D05984C9");

            entity.ToTable("Account");

            entity.HasIndex(e => e.Username, "UQ__Account__536C85E47C2E583F").IsUnique();

            entity.Property(e => e.AccountId)
                .ValueGeneratedNever()
                .HasColumnName("AccountID");
            entity.Property(e => e.CenterId).HasColumnName("CenterID");
            entity.Property(e => e.LastLoginIp)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("LastLoginIP");
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.Center).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.CenterId)
                .HasConstraintName("FK__Account__CenterI__5165187F");

            entity.HasOne(d => d.Role).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__Account__RoleID__5070F446");
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasKey(e => e.PostId).HasName("PK__Announce__AA126038E5DEEA20");

            entity.ToTable("Announcement");

            entity.Property(e => e.PostId).HasColumnName("PostID");
            entity.Property(e => e.PublishDate).HasColumnType("datetime");
            entity.Property(e => e.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PK__Bookings__73951ACD6A796351");

            entity.Property(e => e.BookingId).HasColumnName("BookingID");
            entity.Property(e => e.DropoffAddr).HasMaxLength(255);
            entity.Property(e => e.PassengerId).HasColumnName("PassengerID");
            entity.Property(e => e.PickupAddr).HasMaxLength(255);
            entity.Property(e => e.PickupTime).HasColumnType("datetime");

            entity.HasOne(d => d.Passenger).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.PassengerId)
                .HasConstraintName("FK__Bookings__Passen__6A30C649");
        });

        modelBuilder.Entity<DispatchCenter>(entity =>
        {
            entity.HasKey(e => e.CenterId).HasName("PK__Dispatch__398FC7D7C276E8E8");

            entity.ToTable("DispatchCenter");

            entity.Property(e => e.CenterId)
                .ValueGeneratedNever()
                .HasColumnName("CenterID");
            entity.Property(e => e.CenterName).HasMaxLength(100);
        });

        modelBuilder.Entity<DispatchTask>(entity =>
        {
            entity.HasKey(e => e.TaskId).HasName("PK__Dispatch__7C6949D16E562278");

            entity.Property(e => e.TaskId).HasColumnName("TaskID");
            entity.Property(e => e.ActualArrival).HasColumnType("datetime");
            entity.Property(e => e.BookingId).HasColumnName("BookingID");
            entity.Property(e => e.DriverId).HasColumnName("DriverID");
            entity.Property(e => e.EstimatedArrival).HasColumnType("datetime");
            entity.Property(e => e.VehicleId).HasColumnName("VehicleID");

            entity.HasOne(d => d.Booking).WithMany(p => p.DispatchTasks)
                .HasForeignKey(d => d.BookingId)
                .HasConstraintName("FK__DispatchT__Booki__6EF57B66");

            entity.HasOne(d => d.Driver).WithMany(p => p.DispatchTasks)
                .HasForeignKey(d => d.DriverId)
                .HasConstraintName("FK__DispatchT__Drive__70DDC3D8");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.DispatchTasks)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__DispatchT__Vehic__6FE99F9F");
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.DriverId).HasName("PK__Driver__F1B1CD249998093C");

            entity.ToTable("Driver");

            entity.HasIndex(e => e.DriverNo, "UQ__Driver__F1B1D57631BAEF96").IsUnique();

            entity.Property(e => e.DriverId).HasColumnName("DriverID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.DriverName).HasMaxLength(50);
            entity.Property(e => e.DriverNo)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Mobile)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Account).WithMany(p => p.Drivers)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK__Driver__AccountI__628FA481");
        });

        modelBuilder.Entity<DriverCheckLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__DriverCh__5E5499A8F7036EF3");

            entity.ToTable("DriverCheckLog");

            entity.Property(e => e.LogId).HasColumnName("LogID");
            entity.Property(e => e.Breathalyzer).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.CheckDate).HasColumnType("datetime");
            entity.Property(e => e.DriverId).HasColumnName("DriverID");
            entity.Property(e => e.EndMileage).HasColumnType("decimal(10, 1)");
            entity.Property(e => e.StartMileage).HasColumnType("decimal(10, 1)");
            entity.Property(e => e.VehicleId).HasColumnName("VehicleID");

            entity.HasOne(d => d.Driver).WithMany(p => p.DriverCheckLogs)
                .HasForeignKey(d => d.DriverId)
                .HasConstraintName("FK__DriverChe__Drive__656C112C");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.DriverCheckLogs)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__DriverChe__Vehic__66603565");
        });

        modelBuilder.Entity<DrivingBehavior>(entity =>
        {
            entity.HasKey(e => e.BehaviorId).HasName("PK__DrivingB__361B2187CA256E62");

            entity.ToTable("DrivingBehavior");

            entity.Property(e => e.BehaviorId).HasColumnName("BehaviorID");
            entity.Property(e => e.OccurTime).HasColumnType("datetime");
            entity.Property(e => e.VehicleId).HasColumnName("VehicleID");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.DrivingBehaviors)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__DrivingBe__Vehic__778AC167");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK__Feedback__6A4BEDF6072BEC0F");

            entity.ToTable("Feedback");

            entity.Property(e => e.FeedbackId).HasColumnName("FeedbackID");
            entity.Property(e => e.BookingId).HasColumnName("BookingID");
            entity.Property(e => e.Comment).HasMaxLength(500);

            entity.HasOne(d => d.Booking).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.BookingId)
                .HasConstraintName("FK__Feedback__Bookin__7E37BEF6");
        });

        modelBuilder.Entity<FuelLog>(entity =>
        {
            entity.HasKey(e => e.FuelId).HasName("PK__FuelLog__706CF3C7C8D0279B");

            entity.ToTable("FuelLog");

            entity.Property(e => e.FuelId).HasColumnName("FuelID");
            entity.Property(e => e.FuelType).HasMaxLength(20);
            entity.Property(e => e.Liters).HasColumnType("decimal(7, 2)");
            entity.Property(e => e.VehicleId).HasColumnName("VehicleID");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.FuelLogs)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__FuelLog__Vehicle__01142BA1");
        });

        modelBuilder.Entity<Gpslog>(entity =>
        {
            entity.HasKey(e => e.GpsId).HasName("PK__GPSLogs__E2C2B356169D9321");

            entity.ToTable("GPSLogs");

            entity.HasIndex(e => e.Timestamp, "IX_GPSLogs_Timestamp");

            entity.Property(e => e.GpsId).HasColumnName("GpsID");
            entity.Property(e => e.Latitude).HasColumnType("decimal(10, 7)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(10, 7)");
            entity.Property(e => e.Speed).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Timestamp).HasColumnType("datetime");
            entity.Property(e => e.VehicleId).HasColumnName("VehicleID");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Gpslogs)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__GPSLogs__Vehicle__73BA3083");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__Messages__C87C037C98EAE1BD");

            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.ReceiverId).HasColumnName("ReceiverID");
            entity.Property(e => e.SendTime).HasColumnType("datetime");
            entity.Property(e => e.SenderId).HasColumnName("SenderID");

            entity.HasOne(d => d.Receiver).WithMany(p => p.MessageReceivers)
                .HasForeignKey(d => d.ReceiverId)
                .HasConstraintName("FK__Messages__Receiv__04E4BC85");

            entity.HasOne(d => d.Sender).WithMany(p => p.MessageSenders)
                .HasForeignKey(d => d.SenderId)
                .HasConstraintName("FK__Messages__Sender__03F0984C");
        });

        modelBuilder.Entity<PassengerProfile>(entity =>
        {
            entity.HasKey(e => e.PassengerId).HasName("PK__Passenge__88915F9050D5D69A");

            entity.ToTable("PassengerProfile");

            entity.HasIndex(e => e.IdentityNo, "UQ__Passenge__30655EAE19FB83DF").IsUnique();

            entity.Property(e => e.PassengerId)
                .ValueGeneratedNever()
                .HasColumnName("PassengerID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.AssistiveDevice).HasMaxLength(50);
            entity.Property(e => e.DisabilityLevel).HasMaxLength(20);
            entity.Property(e => e.IdentityNo).HasMaxLength(20);
            entity.Property(e => e.RealName).HasMaxLength(50);

            entity.HasOne(d => d.Account).WithMany(p => p.PassengerProfiles)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK__Passenger__Accou__571DF1D5");
        });

        modelBuilder.Entity<Relationship>(entity =>
        {
            entity.HasKey(e => e.RelId).HasName("PK__Relation__2DA9EE4EBE0813D3");

            entity.ToTable("Relationship");

            entity.Property(e => e.RelId).HasColumnName("RelID");
            entity.Property(e => e.ApplicantName).HasMaxLength(50);
            entity.Property(e => e.PassengerId).HasColumnName("PassengerID");
            entity.Property(e => e.RelationType).HasMaxLength(20);

            entity.HasOne(d => d.Passenger).WithMany(p => p.Relationships)
                .HasForeignKey(d => d.PassengerId)
                .HasConstraintName("FK__Relations__Passe__5AEE82B9");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE3A3848240E");

            entity.Property(e => e.RoleId)
                .ValueGeneratedNever()
                .HasColumnName("RoleID");
            entity.Property(e => e.RoleName).HasMaxLength(20);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.VehicleId).HasName("PK__Vehicle__476B54B25306FB46");

            entity.ToTable("Vehicle");

            entity.HasIndex(e => e.PlateNo, "UQ__Vehicle__48227C0CA32C6C63").IsUnique();

            entity.Property(e => e.VehicleId).HasColumnName("VehicleID");
            entity.Property(e => e.PlateNo)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.VehicleType).HasMaxLength(20);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
