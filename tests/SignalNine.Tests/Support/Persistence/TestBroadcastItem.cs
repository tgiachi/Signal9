using FreeSql.DataAnnotations;

namespace SignalNine.Tests.Support.Persistence;

[Table(Name = "test_broadcast_items")]
public class TestBroadcastItem
{
    [Column(IsPrimary = true)]
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    public int SortOrder { get; set; }
}
