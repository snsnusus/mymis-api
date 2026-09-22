namespace MyMIS.Api.Models;

public class Hobby
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Uppercase version of Name, used ONLY for case-insensitive lookups
    // and to enforce uniqueness at the database level. Never shown to the
    // user — Name keeps whatever casing the first person who typed this
    // hobby actually used (e.g. "Rock Climbing").
    public string NormalizedName { get; set; } = string.Empty;

    // The other side of the many-to-many — a Hobby can belong to many Employees
    public ICollection<EmployeeHobby> EmployeeHobbies { get; set; } = [];
}