using NetromEcoMeal.Entities;

namespace NetromEcoMeal.DTOs;

public record BusinessTypeDto(Guid Id, string Name)
{
    public static BusinessTypeDto FromEntity(BusinessType t) => new(t.Id, t.Name);
}

public record BusinessTypeWriteDto(string Name)
{
    public BusinessType ToEntity(Guid id = default) => new() { Id = id, Name = Name };
}

public record PackageTypeDto(Guid Id, string Name)
{
    public static PackageTypeDto FromEntity(PackageType t) => new(t.Id, t.Name);
}

public record PackageTypeWriteDto(string Name)
{
    public PackageType ToEntity(Guid id = default) => new() { Id = id, Name = Name };
}

public record BrandDto(Guid Id, string Name, string? Description)
{
    public static BrandDto FromEntity(Brand b) => new(b.Id, b.Name, b.Description);
}

public record BrandWriteDto(string Name, string? Description)
{
    public Brand ToEntity(Guid id = default) => new() { Id = id, Name = Name, Description = Description };
}

public record BrandDetailResponseDto(BrandDto Brand, List<BusinessDto> Locations, double? AverageRating, int RatingCount);
