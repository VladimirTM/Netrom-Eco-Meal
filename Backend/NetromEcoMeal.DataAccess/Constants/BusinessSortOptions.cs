namespace NetromEcoMeal.Constants;

// Values are the query-string/select values Home.tsx's sort dropdown passes through.
public static class BusinessSortOptions
{
    public const string Name = "name";
    public const string ClosingSoon = "closingSoon";
    // Requires the customerLat/customerLng pair — see BusinessRepository.GetPagedAsync.
    public const string Distance = "distance";
}
