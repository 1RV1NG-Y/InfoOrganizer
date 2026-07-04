using InfoOrganizer.Domain;
using Microsoft.Extensions.Localization;

namespace InfoOrganizer.Web.Resources;

/// <summary>
/// Localizer-backed display names for the domain enums shown in the UI.
/// These are DISPLAY-ONLY translations; the underlying enum values are what get persisted.
/// </summary>
public static class DisplayNames
{
    public static string Display(this MovementKind kind, IStringLocalizer<AppStrings> l) => kind switch
    {
        MovementKind.In => l["MovementKind_In"],
        MovementKind.Out => l["MovementKind_Out"],
        MovementKind.Adjustment => l["MovementKind_Adjustment"],
        _ => kind.ToString()
    };

    public static string Display(this ReviewRowStatus status, IStringLocalizer<AppStrings> l) => status switch
    {
        ReviewRowStatus.NeedsReview => l["ReviewRowStatus_NeedsReview"],
        ReviewRowStatus.Ready => l["ReviewRowStatus_Ready"],
        ReviewRowStatus.Applied => l["ReviewRowStatus_Applied"],
        ReviewRowStatus.Rejected => l["ReviewRowStatus_Rejected"],
        _ => status.ToString()
    };

    public static string Display(this ImportStatus status, IStringLocalizer<AppStrings> l) => status switch
    {
        ImportStatus.Pending => l["ImportStatus_Pending"],
        ImportStatus.AwaitingReview => l["ImportStatus_AwaitingReview"],
        ImportStatus.Imported => l["ImportStatus_Imported"],
        ImportStatus.Failed => l["ImportStatus_Failed"],
        _ => status.ToString()
    };

    public static string Display(this SourceType source, IStringLocalizer<AppStrings> l) => source switch
    {
        SourceType.Excel => l["SourceType_Excel"],
        SourceType.Image => l["SourceType_Image"],
        SourceType.Csv => l["SourceType_Csv"],
        _ => source.ToString()
    };

    public static string Display(this RecordType type, IStringLocalizer<AppStrings> l) => type switch
    {
        RecordType.Arrivals => l["RecordType_Arrivals"],
        RecordType.Sales => l["RecordType_Sales"],
        RecordType.StockCount => l["RecordType_StockCount"],
        RecordType.Mixed => l["RecordType_Mixed"],
        _ => l["RecordType_Unknown"]
    };

    public static string Display(this CanonicalField field, IStringLocalizer<AppStrings> l) => field switch
    {
        CanonicalField.ProductName => l["CanonicalField_ProductName"],
        CanonicalField.Sku => l["CanonicalField_Sku"],
        CanonicalField.Category => l["CanonicalField_Category"],
        CanonicalField.Unit => l["CanonicalField_Unit"],
        CanonicalField.Quantity => l["CanonicalField_Quantity"],
        CanonicalField.UnitPrice => l["CanonicalField_UnitPrice"],
        CanonicalField.Currency => l["CanonicalField_Currency"],
        CanonicalField.Date => l["CanonicalField_Date"],
        CanonicalField.Location => l["CanonicalField_Location"],
        CanonicalField.PartyName => l["CanonicalField_PartyName"],
        CanonicalField.Direction => l["CanonicalField_Direction"],
        CanonicalField.Note => l["CanonicalField_Note"],
        _ => field.ToString()
    };
}
