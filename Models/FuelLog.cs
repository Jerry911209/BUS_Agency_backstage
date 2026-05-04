using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class FuelLog
{
    public int FuelId { get; set; }

    public int? VehicleId { get; set; }

    public DateOnly? FuelDate { get; set; }

    public string? FuelType { get; set; }

    public decimal? Liters { get; set; }

    public int? Amount { get; set; }

    public virtual Vehicle? Vehicle { get; set; }
}
