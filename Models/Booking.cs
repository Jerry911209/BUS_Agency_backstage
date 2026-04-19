using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class Booking
{
    public long BookingId { get; set; }

    public Guid? PassengerId { get; set; }

    public DateTime PickupTime { get; set; }

    public string? PickupAddr { get; set; }

    public string? DropoffAddr { get; set; }

    public int? CompanionCount { get; set; }

    public int? BookingStatus { get; set; }

    public virtual PassengerProfile? Passenger { get; set; }
}
