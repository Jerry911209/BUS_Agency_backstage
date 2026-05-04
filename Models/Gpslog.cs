using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class Gpslog
{
    public long GpsId { get; set; }

    public int? VehicleId { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public decimal? Speed { get; set; }

    public int? Heading { get; set; }

    public DateTime? Timestamp { get; set; }

    public virtual Vehicle? Vehicle { get; set; }
}
