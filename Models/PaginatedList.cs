using Microsoft.EntityFrameworkCore;

namespace Netrom_Eco_Meal.Models;

// Standard EF Core Skip/Take pagination: one CountAsync for the total, one query for the page.
public class PaginatedList<T>
{
    public List<T> Items { get; }
    public int PageIndex { get; }
    public int TotalPages { get; }
    public int TotalCount { get; }

    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    private PaginatedList(List<T> items, int totalCount, int pageIndex, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageIndex = pageIndex;
        TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
    {
        pageIndex = Math.Max(1, pageIndex);
        var totalCount = await source.CountAsync();
        var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PaginatedList<T>(items, totalCount, pageIndex, pageSize);
    }

    // For queries EF can't translate once projected into T (e.g. ordering a record-constructor
    // projection) — order/page an intermediate shape instead, then map to T after materializing.
    public static async Task<PaginatedList<T>> CreateAsync<TSource>(IQueryable<TSource> source, Func<TSource, T> map, int pageIndex, int pageSize)
    {
        pageIndex = Math.Max(1, pageIndex);
        var totalCount = await source.CountAsync();
        var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PaginatedList<T>(items.Select(map).ToList(), totalCount, pageIndex, pageSize);
    }
}
