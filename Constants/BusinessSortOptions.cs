namespace Netrom_Eco_Meal.Constants;

// Values are the query-string/select values Home.razor's sort dropdown passes through.
public static class BusinessSortOptions
{
    public const string Name = "name";
    public const string ClosingSoon = "closingSoon";
    // Requires the customerLat/customerLng pair — see BusinessRepository.GetPagedAsync.
    public const string Distance = "distance";
}
