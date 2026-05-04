using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class DriverCheckLog
{
    public long LogId { get; set; }

    public int? DriverId { get; set; }

    public int? VehicleId { get; set; }

    public DateTime? CheckDate { get; set; }

    public bool? OilStatus { get; set; }

    public decimal? Breathalyzer { get; set; }

    public decimal? StartMileage { get; set; }

    public decimal? EndMileage { get; set; }

    public bool? WaterStatus { get; set; }

    public bool? SeatbeltStatus { get; set; }

    public string? CheckNote { get; set; }

    public virtual Driver? Driver { get; set; }

    public virtual Vehicle? Vehicle { get; set; }
}
