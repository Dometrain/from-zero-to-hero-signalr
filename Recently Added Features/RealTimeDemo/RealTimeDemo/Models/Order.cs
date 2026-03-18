using System.Text.Json.Serialization;

namespace RealTimeDemo.Models;


[JsonPolymorphic]
[JsonDerivedType(typeof(UrgentOrder), nameof(UrgentOrder))]
[JsonDerivedType(typeof(ReservedOrder), nameof(ReservedOrder))]
public class Order
{
    public string Id { get; set; }
    public string OrderedBy { get; set; }
    public int Amount { get; set; }
}

public class UrgentOrder : Order
{
    public string UrgentBatchId { get; set; }
}

public class ReservedOrder : Order
{
    public int ReserveForDays { get; set; }
}