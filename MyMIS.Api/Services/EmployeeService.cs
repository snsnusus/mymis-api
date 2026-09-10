using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class EmployeeService(AppDbContext context)
{
    private readonly AppDbContext _context = context;
    public async Task<List<EmployeeResponseDto>> GetAllAsync()
    {
        return await _context.Employees
            .Include(e => e.Department)
            .Select(e => new EmployeeResponseDto
            {
                Id = e.Id,
                FirstName = e.FirstName,
                LastName = e.LastName,
                Nickname = e.Nickname,
                Gender = e.Gender,
                MaritalStatus = e.MaritalStatus,
                OfficeLocation = e.OfficeLocation,
                EmployeeCode = e.EmployeeCode,
                DepartmentName = e.Department != null ? e.Department.Name : null
            })
            .ToListAsync();
    }
    public async Task<EmployeeResponseDto?> GetByIdAsync(int id)
    {
        var employee = await _context.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee is null) return null;

        return new EmployeeResponseDto
        {
            Id = employee.Id,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Gender = employee.Gender,
            MaritalStatus = employee.MaritalStatus,
            OfficeLocation = employee.OfficeLocation,
            EmployeeCode = employee.EmployeeCode,
            DepartmentName = employee.Department?.Name,
            Nickname = employee.Nickname,
        };
    }
    public async Task<EmployeeResponseDto> CreateAsync(EmployeeCreateDto dto)
    {
        var employee = new Employee
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Gender = dto.Gender,
            MaritalStatus = dto.MaritalStatus,
            EmployeeCode = dto.EmployeeCode,
            OfficeLocation = dto.OfficeLocation,
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            DepartmentId = dto.DepartmentId
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(employee.Id)
            ?? throw new InvalidOperationException("Failed to reload newly created employee.");
    }
    public async Task<EmployeeResponseDto?> UpdateAsync(int id, EmployeeUpdateDto dto)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee is null) return null;

        employee.FirstName = dto.FirstName;
        employee.LastName = dto.LastName;
        employee.Gender = dto.Gender;
        employee.MaritalStatus = dto.MaritalStatus;
        employee.EmployeeCode = dto.EmployeeCode;
        employee.OfficeLocation = dto.OfficeLocation;
        employee.Username = dto.Username;
        employee.DepartmentId = dto.DepartmentId;

        // Only re-hash if a new password was actually provided
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

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
    public async Task<EmployeeResponseDto?> UpdateSelfAsync(int employeeId, EmployeeSelfUpdateDto dto)
    {
        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee is null) return null;

        employee.Nickname = dto.Nickname;

        await _context.SaveChangesAsync();
        return await GetByIdAsync(employee.Id);
    }
    public async Task<(bool Exists, int? DepartmentId)> GetExistenceAndDepartmentAsync(int id)
    {
        var employee = await _context.Employees.FindAsync(id);
        return employee is null ? (false, null) : (true, employee.DepartmentId);
    }
}