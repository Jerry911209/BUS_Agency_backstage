using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class Vehicle
{
    public int VehicleId { get; set; }

    public string? PlateNo { get; set; }

    public string? VehicleType { get; set; }

    public int? SeatCount { get; set; }

    public int? Status { get; set; }
}
