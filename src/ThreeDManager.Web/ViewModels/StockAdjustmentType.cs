namespace ThreeDManager.Web.ViewModels;

/// <summary>
/// UI-layer manual stock adjustment modes selected on the AdjustStock form.
/// These are not persisted: each maps to a signed stock movement quantity in
/// the controller. Centralized so the view option values, the view-model
/// default, and the controller switch cannot drift into an unrecognized value
/// that would silently fall through to the "Tipo de ajuste inválido" branch.
/// </summary>
public static class StockAdjustmentType
{
    public const string Add = "Add";
    public const string Remove = "Remove";
    public const string Set = "Set";
}
