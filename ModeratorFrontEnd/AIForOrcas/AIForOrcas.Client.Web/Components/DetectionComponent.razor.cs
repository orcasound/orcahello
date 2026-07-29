using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using System.Text.RegularExpressions;

namespace AIForOrcas.Client.Web.Components;

public partial class DetectionComponent
{
	private string _id;
	private string _userId;
	private TextInfo _ti = new CultureInfo("en-US", false).TextInfo;

	[Inject]
	IJSRuntime JSRuntime { get; set; }

	[Inject]
	IAccountService AccountService { get; set; }

	[Inject]
	AuthenticationStateProvider AuthenticationStateProvider { get; set; }

	[Inject]
	NavigationManager NavigationManager { get; set; }

	[Inject]
	UserTagCache TagCache { get; set; }

	[Parameter]
	public Detection Detection { get; set; }

	[Parameter]
	public EventCallback<DetectionUpdate> SubmitCallback { get; set; }

	private string[] optionList = new string[] { "Yes", "No", "Don't Know" };

	private string CardSpectrogramId { get => $"spectrogram-card-{_id}"; }
	private string CardWaveformId { get => $"waveform-card-{_id}"; }
	private string CardPlayButtonId { get => $"play-card-{_id}"; }
	private string CardElapsedTimeId { get => $"elapsed-card-{_id}"; }
	private string CardDurationTimeId { get => $"duration-card-{_id}"; }

	private string ModalSpectrogramPanelId { get => $"spectrogram-panel-modal-{_id}"; }
	private string ModalSpectrogramId { get => $"spectrogram-modal-{_id}"; }
	private string ModalWaveformId { get => $"waveform-modal-{_id}"; }
	private string ModalPlayButtonId { get => $"play-modal-{_id}"; }
	private string ModalElapsedTimeId { get => $"elapsed-modal-{_id}"; }
	private string ModalDurationTimeId { get => $"duration-modal-{_id}"; }

	private string ModalMapPanelId { get => $"map-panel-modal-{_id}"; }
	private string BingMapId { get => $"bingMap-modal-{_id}"; }

	private string ModalLinkId { get => $"link-panel-modal-{_id}"; }

	private string DetectionCount { get => (Detection.Annotations.Count == 1) ? "1 detection" : $"{Detection.Annotations.Count} detections"; }

	private string AverageConfidence { get => $"{Detection.Confidence.ToString("00.##")}% average confidence"; }

	private bool IsSubmitDisabled { get => string.IsNullOrWhiteSpace(Detection.Found); }

	private string WasFound	{ get => _ti.ToTitleCase(Detection.Found); }

	private string LinkUrl { get => $"{NavigationManager.BaseUri}detections/detection/{Detection.Id}"; }

	public List<string> GetSuggestedTagList(Detection d)
	{
		// Add any tags not in TagList that were leaf tags in the most recently moderated detection.
		List<string> suggestedTags = TagCache.GetTags(_userId);

		foreach (var tag in d.SuggestedTagList)
		{
			if (!suggestedTags.Contains(tag))
			{
				suggestedTags.Add(tag);
			}
		}

		return suggestedTags;
	}

	protected override async Task OnParametersSetAsync()
	{
		_id = Detection.Id;

		var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
		var user = authState.User;
		_userId = user.FindFirst("oid")?.Value;

		// Unreviewed detections are being initially populated in the database as "No"
		// I am manually resetting it here when the reviewed status is false so that the record,
		// can be unsubmittable until the user has changed Found to "Yes", "No", or "Don't Know"

		// TODO: Determine whether or not we should change the initial Found state
		//       from No to something other than the three options we give the user

		if (!Detection.Reviewed)
		{
			Detection.Found = string.Empty;

			if (string.IsNullOrEmpty(Detection.Tags))
			{
				if (Detection.GlobalPredictionLabel == "transient")
				{
					AddSuggestedTag("transient");
				}
				else if (Detection.GlobalPredictionLabel == "humpback")
				{
					AddSuggestedTag("humpback");
				}
				else
				{
					AddSuggestedTag("srkw");
				}
			}

			// If Comments is of the form "AI: A and B", then parse out the B and add it too.
			if (!string.IsNullOrEmpty(Detection.Comments))
			{
				var match = Regex.Match(Detection.Comments, @"AI:\s*(?<a>.*?)\s*and\s*(?<b>.*)");
				if (match.Success)
				{
					string b = match.Groups["b"].Value;
					AddSuggestedTag(b);
				}
			}
		}
	}

	private void SetFoundValue(string found)
	{
		Detection.Found = found;

		switch (found)
		{
			case "Yes":
				AddSuggestedTag("srkw");
				break;
			default: // No or Don't Know.
				RemoveTag("srkw");
				break;
		}
	}

	private void AddSuggestedTag(string tag)
	{
		if (string.IsNullOrWhiteSpace(tag))
		{
			return;
		}
		var tagList = Detection.TagList;
		if (tagList.Contains(tag))
		{
			// Nothing to do.
			return;
		}

		// Add the suggested tag before its parent tag if present, otherwise add it to the end of the list.
		Detection.TagHierarchy.TryGetValue(tag, out string parentTag);
		if (!string.IsNullOrWhiteSpace(parentTag))
	        {
			var parentIndex = tagList.IndexOf(parentTag);
			if (parentIndex >= 0)
			{
				tagList.Insert(parentIndex, tag);
			}
			else
			{
				tagList.Add(tag);
			}
		}
		else
		{
			tagList.Add(tag);
		}
		Detection.Tags = string.Join(";", tagList);

		// Add parent tag if not already present.
		if (!string.IsNullOrEmpty(parentTag))
		{
			AddSuggestedTag(parentTag);
		}
	}

	private void RemoveTag(string tag)
	{
		if (string.IsNullOrWhiteSpace(tag))
		{
			return;
		}
		var tagList = Detection.TagList;
		if (!tagList.Contains(tag))
		{
			// Nothing to do.
			return;
		}

		tagList.Remove(tag);
		Detection.Tags = string.Join(";", tagList);

	        // Remove child tags if they exist in the hierarchy.
	        foreach (var pair in Detection.TagHierarchy)
		{
			if (pair.Value == tag)
			{
				RemoveTag(pair.Key);
			}
		}
	}

	private async Task SubmitUpdate()
	{
		var request = new DetectionUpdate()
		{
			Id = Detection.Id,
			Comments = Detection.Comments,
			Tags = Detection.Tags,
			Moderator = await AccountService.GetUsername(),
			Moderated = DateTime.Now,
			Reviewed = true,
			Found = Detection.Found
		};

		await SubmitCallback.InvokeAsync(request);
	}

	private async Task ToggleCardPlayer()
	{
		await JSRuntime.InvokeVoidAsync("CardSpectrogram", _id, Detection.AudioUri);
	}

	private async Task ToggleModalPlayer()
	{
		var isPlaying = await JSRuntime.InvokeAsync<bool>("IsPlayerActive");

		if (!isPlaying)
		{
			await InitializeModalPlayer();
		}

		await JSRuntime.InvokeVoidAsync("ToggleModalSpectrogram");
	}

	private async Task InitializeModalPlayer()
	{
		StringBuilder sb = new StringBuilder();
		sb.Append("[");

		var list = new List<string>();
		foreach(var annotation in Detection.Annotations)
		{
			var entry = "{";
			entry += $"\"start\":{annotation.StartTime}, ";
			entry += $"\"end\":{annotation.EndTime},";
			entry += "\"drag\": false";
			entry += "}";
			list.Add(entry);
		}
		sb.Append(string.Join(",", list));

		sb.AppendLine("]");

		await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
		await JSRuntime.InvokeVoidAsync("InitializeModalSpectrogram", _id, 
			Detection.AudioUri, sb.ToString());
	}

	private async Task InitializeModalMap()
	{
		await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
		await JSRuntime.InvokeVoidAsync("LoadBingMap", _id, 
			Detection.Location?.Latitude, Detection.Location?.Longitude);
	}

	private async Task KillPlayer()
	{
		await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
	}

	private async Task ActivateLink(string url)
	{
		var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();

		NavigationManager.NavigateTo(url, true);
	}
}
