using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BUS_Agency_backstage.Models;

public partial class Vehicle
{
    public int VehicleId { get; set; }

    public string PlateNo { get; set; } = null!;

    public string? VehicleType { get; set; }

    public int? SeatCount { get; set; }

    public int? Status { get; set; } // 0:正常, 1:維修中, 2:報廢

    // 🌟 增加這個屬性，方便你在 Controller 和 View 使用
    [NotMapped]
    public bool IsAvailable => Status == 0;

    public virtual ICollection<DispatchTask> DispatchTasks { get; set; } = new List<DispatchTask>();

    public virtual ICollection<DriverCheckLog> DriverCheckLogs { get; set; } = new List<DriverCheckLog>();

    public virtual ICollection<DrivingBehavior> DrivingBehaviors { get; set; } = new List<DrivingBehavior>();

    public virtual ICollection<FuelLog> FuelLogs { get; set; } = new List<FuelLog>();

    public virtual ICollection<Gpslog> Gpslogs { get; set; } = new List<Gpslog>();

}
