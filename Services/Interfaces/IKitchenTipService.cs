using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

public interface IKitchenTipService
{
    public Task<List<KitchenTip>> GetVisibleByBusinessIdAsync(Guid businessId);
    public Task<Dictionary<Guid, string>> GetSnippetsByIdsAsync(IEnumerable<Guid> ids);
    public Task<KitchenTip> SubmitAsync(Guid businessId, string tip);
    // Mirrors IBusinessService/IPackageService.HideAsync — called from ReportService.TakeActionAsync
    // when an open report's target is a KitchenTip. notify:false lets the caller send its own
    // notification after committing the surrounding transaction, same reason those two do.
    public Task<KitchenTip?> HideAsync(Guid tipId, string reason, bool notify = true);
    public Task NotifyHiddenAsync(KitchenTip tip, string reason);
}
