using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.Models;
using System.Globalization;

namespace MyMIS.Api.Services;

public class HobbyService(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<Hobby> GetOrCreateHobbyAsync(string hobbyName)
    {
        var trimmedName = hobbyName.Trim();

        if (string.IsNullOrEmpty(trimmedName))
        {
            throw new ArgumentException("Hobby name cannot be empty.", nameof(hobbyName));
        }

        var normalizedName = trimmedName.ToUpperInvariant();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var existingHobby = await _context.Hobbies
                .FirstOrDefaultAsync(h => h.NormalizedName == normalizedName);

            if (existingHobby is not null)
            {
                return existingHobby;
            }

            var newHobby = new Hobby
            {
                Name = ToTitleCase(trimmedName),
                NormalizedName = normalizedName
            };
            _context.Hobbies.Add(newHobby);

            try
            {
                await _context.SaveChangesAsync();
                return newHobby;
            }
            catch (DbUpdateException)
            {
                _context.Entry(newHobby).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException(
            "Could not resolve hobby after multiple attempts — repeated write conflicts.");
    }

    // "reading a book" -> "Reading A Book". Capitalizes the first letter of
    // every word; everything else gets lowercased first, deliberately —
    // see the note below on why that first step matters.
    private static string ToTitleCase(string input)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
    }
}