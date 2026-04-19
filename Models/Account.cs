using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class Account
{
    public Guid AccountId { get; set; }

    public string Username { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public int? RoleId { get; set; }

    public int? CenterId { get; set; }

    public bool? IsLocked { get; set; }

    public virtual DispatchCenter? Center { get; set; }

    public virtual ICollection<Driver> Drivers { get; set; } = new List<Driver>();

    public virtual ICollection<PassengerProfile> PassengerProfiles { get; set; } = new List<PassengerProfile>();

    public virtual Role? Role { get; set; }
}
