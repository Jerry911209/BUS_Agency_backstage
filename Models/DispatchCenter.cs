using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class DispatchCenter
{
    public int CenterId { get; set; }

    public string? CenterName { get; set; }

    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();
}
