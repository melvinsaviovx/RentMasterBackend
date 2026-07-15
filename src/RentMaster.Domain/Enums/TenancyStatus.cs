namespace RentMaster.Domain.Enums;

public enum TenancyStatus
{
    PendingTenantConfirmation = 1,
    Active = 2,
    EndRequested = 3,
    Ended = 4,
    Cancelled = 5
}
