namespace AIForOrcas.Client.Web.Pages.Detections;

public partial class SingleDetection : ComponentBase, IDisposable
{
	[Parameter]
	public string Id { get; set; }

	[Inject]
	IJSRuntime JSRuntime { get; set; }

	[Inject]
	IDetectionService Service { get; set; }

	[Inject]
	IToastService ToastService { get; set; }

	[Inject]
	UserTagCache TagCache { get; set; }

	[Inject]
	AuthenticationStateProvider AuthenticationStateProvider { get; set; }

	private string _userId;
	private Detection detection = null;
	private bool isFound = true;
	private bool isUnavailable = false;

	protected override async Task OnInitializedAsync()
	{
		await LoadDetection();

		var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
		var user = authState.User;
		_userId = user.FindFirst("oid")?.Value;
	}

	private async Task LoadDetection()
	{
		detection = await Service.GetDetectionAsync(Id);
		isUnavailable = detection == null;
		if (!isUnavailable && detection.Id == null)
			isFound = false;
	}

	private async Task ActOnSubmitCallback(DetectionUpdate request)
	{
		await Service.UpdateRequestAsync(request);

		List<string> leafTags = Detection.GetLeafTags(request.Tags);
		TagCache.SetTags(_userId, leafTags);

		ToastService.ShowSuccess("Detection successfully updated.");

		await LoadDetection();
	}

	void IDisposable.Dispose()
	{
		JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
	}
}
