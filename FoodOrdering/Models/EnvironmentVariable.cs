using System.ComponentModel;

namespace FoodOrdering.Models;

public class EnvironmentVariable
{
    public EnvironmentVariable(string name = "", string value = "", string comment = "")
    {
        Name = name;
        Value = value;
        Comment = comment;
    }

    public string Name { get; set; }
    public string Value { get; set; }
    public string Comment { get; set; }
}