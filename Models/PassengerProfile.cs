using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class PassengerProfile
{
    public Guid PassengerId { get; set; }

    public Guid? AccountId { get; set; }

    public string? RealName { get; set; }

    public string? IdentityNo { get; set; }

    public int? IdentityType { get; set; }

    public string? DisabilityLevel { get; set; }

    public DateOnly? BirthDate { get; set; }

    public int? AuditStatus { get; set; }

    public virtual Account? Account { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
