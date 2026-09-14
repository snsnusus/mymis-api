namespace MyMIS.Api.Models;

// The explicit join entity for the Employee <-> Hobby many-to-many.
// Notice there's no `Id` property here — that's deliberate. This table
// has no identity of its own beyond "this Employee has this Hobby," so
// the combination of the two foreign keys IS the primary key (a composite key),
// configured in AppDbContext below. This also means the same employee
// can't have the same hobby listed twice — the composite key enforces that
// at the database level, not just in application code.
public class EmployeeHobby
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int HobbyId { get; set; }
    public Hobby Hobby { get; set; } = null!;
}