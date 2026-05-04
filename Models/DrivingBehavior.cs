using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class DrivingBehavior
{
    public long BehaviorId { get; set; }

    public int? VehicleId { get; set; }

    public int? BehaviorType { get; set; }

    public DateTime? OccurTime { get; set; }

    public virtual Vehicle? Vehicle { get; set; }
}
