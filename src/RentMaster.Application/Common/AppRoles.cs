namespace RentMaster.Application.Common;

public static class AppRoles
{
    public const string Owner = "Owner";
    public const string Tenant = "Tenant";
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Maintenance = "Maintenance";

    public static readonly string[] All = [Owner, Tenant, Admin, Moderator, Maintenance];
    public static readonly string[] SelfRegisterable = [Owner, Tenant];
}
