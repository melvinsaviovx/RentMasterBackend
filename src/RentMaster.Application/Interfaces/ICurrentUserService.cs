namespace RentMaster.Application.Interfaces;

public interface ICurrentUserService
{
    string UserId { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
