using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class Message
{
    public long MessageId { get; set; }

    public Guid? SenderId { get; set; }

    public Guid? ReceiverId { get; set; }

    public int? MessageType { get; set; }

    public string? Content { get; set; }

    public DateTime? SendTime { get; set; }

    public virtual Account? Receiver { get; set; }

    public virtual Account? Sender { get; set; }
}
