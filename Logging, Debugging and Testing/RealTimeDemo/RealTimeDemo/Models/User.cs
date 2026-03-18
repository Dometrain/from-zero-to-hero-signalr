namespace RealTimeDemo.Models;

public class User
{
    public string Name { get; set; }
    public bool IsOnline { get; set; }
    public List<string> Groups { get; set; }
}