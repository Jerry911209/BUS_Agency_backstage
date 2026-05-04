using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class Feedback
{
    public int FeedbackId { get; set; }

    public long? BookingId { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public virtual Booking? Booking { get; set; }
}
