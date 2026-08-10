using System.ComponentModel.DataAnnotations.Schema;
using Netrom_Eco_Meal.Constants;

namespace Netrom_Eco_Meal.Entities;

public class Business
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Address { get; set; }
    public string? ImageUrl { get; set; }
    // Optional — powers "near me" distance sort and the map view. Null skips both.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    // See Constants.BusinessStatuses. Admin-created businesses default straight to Approved;
    // self-service applications (SubmittedByUserId set) start at PendingApproval.
    public string Status { get; set; } = BusinessStatuses.Approved;
    public string? RejectionReason { get; set; }
    // Independent of Status — an admin moderation flag that can hide an otherwise-Approved
    // business from the storefront without touching its approval state.
    public bool IsHidden { get; set; }
    public string? HiddenReason { get; set; }
    // Who submitted this business for approval — null for businesses an admin created directly.
    public string? SubmittedByUserId { get; set; }
    public Guid BusinessTypeId { get; set; }
    [ForeignKey(nameof(BusinessTypeId))]
    public BusinessType BusinessType { get; set; } = null!;
    public ICollection<BusinessStaff> Staff { get; set; } = [];
    public ICollection<Package> Packages { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
    public ICollection<Favorite> Favorites { get; set; } = [];
    public ICollection<BusinessHours> Hours { get; set; } = [];
    public ICollection<BusinessClosure> Closures { get; set; } = [];
}