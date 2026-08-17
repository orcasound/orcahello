namespace OrcaHello.Web.UI.Pages.Components
{
    [ExcludeFromCodeCoverage]
    public partial class AuthenticationComponent
    {
        [Inject]
        IAccountService AccountService { get; set; } = null!;

        private string DisplayName { get; set; } = string.Empty;
        private System.Timers.Timer _timer = null!;

        protected override async Task OnInitializedAsync()
        {
            DisplayName = await AccountService.GetDisplayName();
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                // We are setting a timer here to periodically verify
                // the user's token has not expired. Right now we are checking once
                // a minute. We can adjust it to longer if need be.

                _timer = new System.Timers.Timer(60000);
                _timer.Elapsed += OnTimerElapsed;
                _timer.Start();
            }
        }
        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            Task.Run(async () => await AccountService.LogoutIfExpired()).Wait();
        }

        private async Task Login()
        {
            await AccountService.Login();
            DisplayName = await AccountService.GetDisplayName();
        }

        private async Task OnProfileMenuClicked(RadzenProfileMenuItem item)
        {
            if (item.Text == "Log Out")
            {
                bool? result = await DialogService.Confirm("Select \"Log Out\" below if you are ready to end your current session.", "Ready to Leave?", new ConfirmOptions() { OkButtonText = "Log Out", CancelButtonText = "Cancel" });

                if (result == true)
                {
                    await AccountService.Logout();
                    DisplayName = string.Empty;
                }
            }
        }
    }
}
