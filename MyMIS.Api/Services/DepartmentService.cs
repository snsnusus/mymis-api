using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class DepartmentService(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<List<DepartmentResponseDto>> GetAllAsync()
    {
        return await _context.Departments
            .Include(d => d.PrimaryContact)
            .Include(d => d.SecondaryContact)
            .Select(d => new DepartmentResponseDto
            {
                Id = d.Id,
                Name = d.Name,
                Slug = d.Slug,
                Description = d.Description,
                Status = d.Status,
                PrimaryContactName = d.PrimaryContact != null
                    ? d.PrimaryContact.FirstName + " " + d.PrimaryContact.LastName
                    : null,
                SecondaryContactName = d.SecondaryContact != null
                    ? d.SecondaryContact.FirstName + " " + d.SecondaryContact.LastName
                    : null,
                EmployeeCount = d.Employees.Count
            })
            .ToListAsync();
    }

    public async Task<DepartmentResponseDto?> GetByIdAsync(int id)
    {
        return await _context.Departments
            .Include(d => d.PrimaryContact)
            .Include(d => d.SecondaryContact)
            .Where(d => d.Id == id)
            .Select(d => new DepartmentResponseDto
            {
                Id = d.Id,
                Name = d.Name,
                Slug = d.Slug,
                Description = d.Description,
                Status = d.Status,
                PrimaryContactName = d.PrimaryContact != null
                    ? d.PrimaryContact.FirstName + " " + d.PrimaryContact.LastName
                    : null,
                SecondaryContactName = d.SecondaryContact != null
                    ? d.SecondaryContact.FirstName + " " + d.SecondaryContact.LastName
                    : null,
                EmployeeCount = d.Employees.Count
            })
            .FirstOrDefaultAsync();
    }

    public async Task<DepartmentResponseDto> CreateAsync(DepartmentCreateDto dto)
    {
        var department = new Department
        {
            Name = dto.Name,
            Slug = dto.Slug,
            Description = dto.Description,
            PrimaryContactId = dto.PrimaryContactId,
            SecondaryContactId = dto.SecondaryContactId,
            Status = dto.Status
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(department.Id)
            ?? throw new InvalidOperationException("Failed to reload newly created department.");
    }

    public async Task<DepartmentResponseDto?> UpdateAsync(int id, DepartmentCreateDto dto)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department is null) return null;

        department.Name = dto.Name;
        department.Slug = dto.Slug;
        department.Description = dto.Description;
        department.PrimaryContactId = dto.PrimaryContactId;
        department.SecondaryContactId = dto.SecondaryContactId;
        department.Status = dto.Status;

        await _context.SaveChangesAsync();
        return await GetByIdAsync(department.Id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department is null) return false;

        _context.Departments.Remove(department);
        await _context.SaveChangesAsync();
        return true;
    }
}