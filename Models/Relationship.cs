using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class Relationship
{
    public int RelId { get; set; }

    public Guid? PassengerId { get; set; }

    public string? ApplicantName { get; set; }

    public string? RelationType { get; set; }

    public virtual PassengerProfile? Passenger { get; set; }
}
