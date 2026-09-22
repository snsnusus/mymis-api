using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class EmployeeService(AppDbContext context, HobbyService hobbyService)
{
  private readonly AppDbContext _context = context;
  private readonly HobbyService _hobbyService = hobbyService;
  public async Task<List<EmployeeSummaryResponseDto>> GetAllAsync()
  {
    return await _context.Employees
        .Include(e => e.Department)
        .Select(e => new EmployeeSummaryResponseDto
        {
          Id = e.Id,
          FirstName = e.FirstName,
          MiddleName = e.MiddleName,
          LastName = e.LastName,
          Suffix = e.Suffix,
          EmployeeCode = e.EmployeeCode,
          DepartmentName = e.Department != null ? e.Department.Name : null,
          AvatarUrl = e.AvatarUrl,
          PositionTitle = e.Position != null ? e.Position.Title : null

        })
        .ToListAsync();
  }
  public async Task<EmployeeResponseDto?> GetByIdAsync(int id)
  {
    var employee = await _context.Employees
        .Include(e => e.Department)
        .Include(e => e.PersonalDetail)
        .Include(e => e.Position)
        .Include(e => e.EmployeeHobbies)
            .ThenInclude(eh => eh.Hobby)
        .FirstOrDefaultAsync(e => e.Id == id);

    if (employee is null) return null;

    return new EmployeeResponseDto
    {
      Id = employee.Id,
      FirstName = employee.FirstName,
      MiddleName = employee.MiddleName,
      LastName = employee.LastName,
      Suffix = employee.Suffix,
      AvatarUrl = employee.AvatarUrl,
      Gender = employee.Gender,
      Birthdate = employee.Birthdate,
      MaritalStatus = employee.MaritalStatus,
      OfficeLocation = employee.OfficeLocation,
      WorkSchedule = employee.WorkSchedule,
      EmployeeCode = employee.EmployeeCode,
      Username = employee.Username,
      DepartmentId = employee.DepartmentId,
      DepartmentName = employee.Department?.Name,
      PersonalDetail = employee.PersonalDetail is null ? null : new EmployeePersonalDetailDto
      {
        Nickname = employee.PersonalDetail.Nickname,
        Birthplace = employee.PersonalDetail.Birthplace,
        Nationality = employee.PersonalDetail.Nationality,
        BloodType = employee.PersonalDetail.BloodType,
        Religion = employee.PersonalDetail.Religion,
        Bio = employee.PersonalDetail.Bio
      },
      Position = employee.Position is null ? null : new EmployeePositionDto
      {
        Id = employee.Position.Id,
        Title = employee.Position.Title,
        Slug = employee.Position.Slug,
        Description = employee.Position.Description,
        IsActive = employee.Position.IsActive,
        IsApprover = employee.Position.IsApprover
      },
      Hobbies = [.. employee.EmployeeHobbies.Select(eh => new HobbyResponseDto { Id = eh.Hobby.Id, Name = eh.Hobby.Name })]
    };
  }
  public async Task<EmployeeResponseDto> CreateAsync(EmployeeCreateDto dto)
  {
    var employee = new Employee
    {
      FirstName = dto.FirstName,
      MiddleName = dto.MiddleName,
      LastName = dto.LastName,
      Suffix = dto.Suffix,
      AvatarUrl = dto.AvatarUrl,
      Gender = dto.Gender,
      Birthdate = dto.Birthdate,
      MaritalStatus = dto.MaritalStatus,
      EmployeeCode = dto.EmployeeCode,
      OfficeLocation = dto.OfficeLocation,
      WorkSchedule = dto.WorkSchedule,
      Username = dto.Username,
      PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
      DepartmentId = dto.DepartmentId,
      PositionId = dto.PositionId,
      PersonalDetail = new EmployeePersonalDetail
      {
        Nickname = dto.PersonalDetail?.Nickname,
        Birthplace = dto.PersonalDetail?.Birthplace,
        Nationality = dto.PersonalDetail?.Nationality,
        BloodType = dto.PersonalDetail?.BloodType,
        Religion = dto.PersonalDetail?.Religion,
        Bio = dto.PersonalDetail?.Bio
      }
    };

    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    return await GetByIdAsync(employee.Id)
        ?? throw new InvalidOperationException("Failed to reload newly created employee.");
  }
  public async Task<EmployeeResponseDto?> UpdateAsync(int id, EmployeeUpdateDto dto)
  {
    var employee = await _context.Employees
        .Include(e => e.PersonalDetail)
        .FirstOrDefaultAsync(e => e.Id == id);

    if (employee is null) return null;

    employee.FirstName = dto.FirstName;
    employee.MiddleName = dto.MiddleName;
    employee.LastName = dto.LastName;
    employee.Suffix = dto.Suffix;
    employee.AvatarUrl = dto.AvatarUrl;
    employee.Gender = dto.Gender;
    employee.Birthdate = dto.Birthdate;
    employee.MaritalStatus = dto.MaritalStatus;
    employee.EmployeeCode = dto.EmployeeCode;
    employee.OfficeLocation = dto.OfficeLocation;
    employee.WorkSchedule = dto.WorkSchedule;
    employee.Username = dto.Username;

    // Only re-hash if a new password was actually provided
    if (!string.IsNullOrWhiteSpace(dto.Password))
    {
      employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
    }

    employee.PersonalDetail ??= new EmployeePersonalDetail();
    employee.PersonalDetail.Nickname = dto.PersonalDetail?.Nickname;
    employee.PersonalDetail.Birthplace = dto.PersonalDetail?.Birthplace;
    employee.PersonalDetail.Nationality = dto.PersonalDetail?.Nationality;
    employee.PersonalDetail.BloodType = dto.PersonalDetail?.BloodType;
    employee.PersonalDetail.Religion = dto.PersonalDetail?.Religion;
    employee.PersonalDetail.Bio = dto.PersonalDetail?.Bio;

    await _context.SaveChangesAsync();
    return await GetByIdAsync(employee.Id);
  }

  public async Task<EmployeeResponseDto?> UpdateSelfAsync(int employeeId, EmployeeSelfUpdateDto dto)
  {
    var employee = await _context.Employees
    .Include(e => e.PersonalDetail)
    .FirstOrDefaultAsync(e => e.Id == employeeId);

    if (employee is null) return null;

    employee.PersonalDetail ??= new EmployeePersonalDetail();
    employee.PersonalDetail.Nickname = dto.Nickname;
    employee.PersonalDetail.Birthplace = dto.Birthplace;
    employee.PersonalDetail.Nationality = dto.Nationality;
    employee.PersonalDetail.BloodType = dto.BloodType;
    employee.PersonalDetail.Religion = dto.Religion;
    employee.PersonalDetail.Bio = dto.Bio;

    await _context.SaveChangesAsync();
    return await GetByIdAsync(employee.Id);
  }

  public async Task<EmployeeResponseDto?> UpdatePartialAsync(int id, EmployeePartialUpdateDto dto)
  {
    var employee = await _context.Employees.FindAsync(id);
    if (employee is null) return null;

    employee.OfficeLocation = dto.OfficeLocation;
    employee.WorkSchedule = dto.WorkSchedule;

    await _context.SaveChangesAsync();
    return await GetByIdAsync(employee.Id);
  }

  public async Task<bool> DeleteAsync(int id)
  {
    var employee = await _context.Employees.FindAsync(id);
    if (employee is null) return false;

    employee.DeletedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();
    return true;
  }

  public async Task<(bool Exists, int? DepartmentId)> GetExistenceAndDepartmentAsync(int id)
  {
    var employee = await _context.Employees.FindAsync(id);
    return employee is null ? (false, null) : (true, employee.DepartmentId);
  }

  public async Task<HobbyResponseDto?> LinkHobbyAsync(int employeeId, string hobbyName)
  {
    var employeeExists = await _context.Employees.AnyAsync(e => e.Id == employeeId);
    if (!employeeExists) return null;

    var hobby = await _hobbyService.GetOrCreateHobbyAsync(hobbyName);

    var alreadyLinked = await _context.EmployeeHobbies
        .AnyAsync(eh => eh.EmployeeId == employeeId && eh.HobbyId == hobby.Id);

    if (!alreadyLinked)
    {
      _context.EmployeeHobbies.Add(new EmployeeHobby
      {
        EmployeeId = employeeId,
        HobbyId = hobby.Id
      });
      await _context.SaveChangesAsync();
    }

    return new HobbyResponseDto { Id = hobby.Id, Name = hobby.Name };
  }

  public async Task<bool> UnlinkHobbyAsync(int employeeId, int hobbyId)
  {
    var link = await _context.EmployeeHobbies
        .FirstOrDefaultAsync(eh => eh.EmployeeId == employeeId && eh.HobbyId == hobbyId);

    if (link is null) return false;

    _context.EmployeeHobbies.Remove(link);
    await _context.SaveChangesAsync();
    return true;
  }
}