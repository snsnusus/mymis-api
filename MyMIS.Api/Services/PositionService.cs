using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class PositionService(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<List<PositionResponseDto>> GetAllAsync()
    {
        return await _context.Positions
            .Include(d => d.Department)
            .Select(d => new PositionResponseDto
            {
                Id = d.Id,
                Title = d.Title,
                Slug = d.Slug,
                Description = d.Description,
                SortOrder = d.SortOrder,
                IsActive = d.IsActive,
                IsApprover = d.IsApprover,
                DepartmentId = d.DepartmentId,
                DepartmentName = d.Department.Name
            })
            .ToListAsync();
    }

    public async Task<PositionResponseDto?> GetByIdAsync(int id)
    {
        return await _context.Positions
            .Include(d => d.Department)
            .Where(d => d.Id == id)
            .Select(d => new PositionResponseDto
            {
                Id = d.Id,
                Title = d.Title,
                Slug = d.Slug,
                Description = d.Description,
                SortOrder = d.SortOrder,
                IsActive = d.IsActive,
                IsApprover = d.IsApprover,
                DepartmentId = d.DepartmentId,
                DepartmentName = d.Department.Name
            })
            .FirstOrDefaultAsync();
    }

    public async Task<PositionResponseDto> CreateAsync(PositionCreateDto dto)
    {
        var position = new Position
        {
            Title = dto.Title,
            Slug = dto.Slug,
            Description = dto.Description,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
            IsApprover = dto.IsApprover,
            DepartmentId = dto.DepartmentId,
        };
        _context.Positions.Add(position);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(position.Id)
            ?? throw new InvalidOperationException("Failed to reload newly created position.");
    }

    public async Task<PositionResponseDto?> UpdateAsync(int id, PositionCreateDto dto)
    {
        var position = await _context.Positions.FindAsync(id);
        if (position is null) return null;

        position.Title = dto.Title;
        position.Slug = dto.Slug;
        position.Description = dto.Description;
        position.SortOrder = dto.SortOrder;
        position.IsActive = dto.IsActive;
        position.IsApprover = dto.IsApprover;
        position.DepartmentId = dto.DepartmentId;

        await _context.SaveChangesAsync();
        return await GetByIdAsync(position.Id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var position = await _context.Positions.FindAsync(id);
        if (position is null) return false;

        _context.Positions.Remove(position);
        await _context.SaveChangesAsync();
        return true;
    }
}