using System;
using System.Collections.Generic;

namespace BUS_Agency_backstage.Models;

public partial class Announcement
{
    public int PostId { get; set; }

    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    public DateTime? PublishDate { get; set; }
}
