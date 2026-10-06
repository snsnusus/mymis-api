using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Helpers;

public static class AddressMapper
{
  // Request DTO → owned value. Callers must have validated BarangayId first.
  public static Address ToEntity(AddressDto dto) => new()
  {
    AddressLine1 = dto.AddressLine1.Trim(),
    AddressLine2 = string.IsNullOrWhiteSpace(dto.AddressLine2) ? null : dto.AddressLine2.Trim(),
    BarangayId = dto.BarangayId!.Value,
    PostalCode = dto.PostalCode.Trim(),
  };

  // Owned value → response. Requires Barangay.City.Region to be loaded (Include/ThenInclude).
  public static AddressResponseDto ToResponse(Address address) => new()
  {
    AddressLine1 = address.AddressLine1,
    AddressLine2 = address.AddressLine2,
    PostalCode = address.PostalCode,
    Barangay = new LocationRefDto { Id = address.Barangay.Id, Name = address.Barangay.Name },
    City = new LocationRefDto { Id = address.Barangay.City.Id, Name = address.Barangay.City.Name },
    Region = new LocationRefDto { Id = address.Barangay.City.Region.Id, Name = address.Barangay.City.Region.Name },
  };
}