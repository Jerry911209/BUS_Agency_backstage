using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class Booking
{
    public long BookingId { get; set; }

    public Guid? PassengerId { get; set; }

    public int? BookingType { get; set; }

    public DateTime? PickupTime { get; set; }

    public string? PickupAddr { get; set; }

    public string? DropoffAddr { get; set; }

    public int? CompanionCount { get; set; }

    public int? BookingStatus { get; set; }

    public bool? IsRoundTrip { get; set; }

    public DateTime? CreatedAt { get; set; } // 或是 CreatedAt，請依據妳實際產生的欄位名稱為準
    public virtual ICollection<DispatchTask> DispatchTasks { get; set; } = new List<DispatchTask>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual PassengerProfile? Passenger { get; set; }
}
