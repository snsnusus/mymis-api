namespace MyMIS.Api.Models;

public class Hobby
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // The other side of the many-to-many — a Hobby can belong to many Employees
    public ICollection<EmployeeHobby> EmployeeHobbies { get; set; } = [];
}