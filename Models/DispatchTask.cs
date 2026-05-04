using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class DispatchTask
{
    public long TaskId { get; set; }

    public long? BookingId { get; set; }

    public int? VehicleId { get; set; }

    public int? DriverId { get; set; }

    public DateTime? EstimatedArrival { get; set; }

    public DateTime? ActualArrival { get; set; }

    public int? CollectAmount { get; set; }

    public int? SubsidyAmount { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual Driver? Driver { get; set; }

    public virtual Vehicle? Vehicle { get; set; }
}
