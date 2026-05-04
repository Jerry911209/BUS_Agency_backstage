using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class Driver
{
    public int DriverId { get; set; }

    public Guid? AccountId { get; set; }

    public string DriverNo { get; set; } = null!;

    public string? DriverName { get; set; }

    public string? Mobile { get; set; }

    public virtual Account? Account { get; set; }

    public virtual ICollection<DispatchTask> DispatchTasks { get; set; } = new List<DispatchTask>();

    public virtual ICollection<DriverCheckLog> DriverCheckLogs { get; set; } = new List<DriverCheckLog>();
}
