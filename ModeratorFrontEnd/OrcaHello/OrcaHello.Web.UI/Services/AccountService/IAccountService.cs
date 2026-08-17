namespace OrcaHello.Web.UI.Services
{
    public interface IAccountService
    {
        Task Login();
        Task Logout();
        Task LogoutIfExpired();
        Task<string> GetDisplayName();
        Task<string> GetUserName();
    }
}
