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
		var suggestedTags = new List<string>();

		// Add any tags not in TagList that were leaf tags in the most recently moderated detection.
		foreach (var tag in TagCache.GetTags(_userId))
		{
			if (!d.TagList.Contains(tag, StringComparer.OrdinalIgnoreCase))
			{
				suggestedTags.Add(tag);
			}
		}

		foreach (var tag in d.SuggestedTagList)
		{
			if (!suggestedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
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
					AddTag("transient");
				}
				else if (Detection.GlobalPredictionLabel == "humpback")
				{
					AddTag("humpback");
				}

				// Don't add the "srkw" tag here because we want the user
				// to explicitly select it if they see it in the audio.
			}

			// If Comments is of the form "AI: A and B", then parse out the B and add it too.
			if (!string.IsNullOrEmpty(Detection.Comments))
			{
				var match = Regex.Match(Detection.Comments, @"AI:\s*(?<a>.*?)\s*and\s*(?<b>.*)");
				if (match.Success)
				{
					string b = match.Groups["b"].Value;
					AddTag(b);
				}
			}
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		// Invoked on every render because the card may not be in the DOM yet on the
		// first render (e.g. while the single detection page is still loading the record);
		// the JS side is idempotent and exits early once the shades exist.
		await JSRuntime.InvokeVoidAsync("DrawRegionShades", _id, Detection.AudioUri, RegionsJson);
	}

	private void SetFoundValue(string found)
	{
		Detection.Found = found;

		switch (found)
		{
			case "Yes":
				AddTag("srkw");
				break;
			default: // No or Don't Know.
				RemoveTag("srkw");
				break;
		}
	}

	/// <summary>
	/// Add a tag to the detection's tag list, ensuring that it is added before its parent tag if present, and also adding the parent tag if not already present.
	/// If the tag is "srkw" and the Found value is not "Yes", it sets Found to "Yes".
	/// </summary>
	/// <param name="tag">Tag to add</param>
	private void AddTag(string tag)
	{
		if (string.IsNullOrWhiteSpace(tag))
		{
			return;
		}
		var tagList = Detection.TagList;
		if (tagList.Contains(tag, StringComparer.OrdinalIgnoreCase))
		{
			// Nothing to do.
			return;
		}

		// Add the suggested tag before its parent tag if present, otherwise add it to the end of the list.
		Detection.TagHierarchy.TryGetValue(tag, out string parentTag);
		if (!string.IsNullOrWhiteSpace(parentTag))
	        {
			var parentIndex = tagList.FindIndex(t => t.Equals(parentTag, StringComparison.OrdinalIgnoreCase));
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
			AddTag(parentTag);
		}

		if (tag.Equals("srkw", StringComparison.OrdinalIgnoreCase) && Detection.Found != "Yes")
		{
			SetFoundValue("Yes");
		}
	}

	/// <summary>
	/// Remove a tag from the detection's tag list.
	/// Also remove any child tags that have this tag as their parent in the hierarchy.
	/// </summary>
	/// <param name="tag">Tag to remove</param>
	private void RemoveTag(string tag)
	{
		if (string.IsNullOrWhiteSpace(tag))
		{
			return;
		}
		var tagList = Detection.TagList;
		if (!tagList.Contains(tag, StringComparer.OrdinalIgnoreCase))
		{
			// Nothing to do.
			return;
		}

		tagList.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
		Detection.Tags = string.Join(";", tagList);

		// Remove child tags if they exist in the hierarchy.
		foreach (var pair in Detection.TagHierarchy)
		{
			if ((pair.Value != null) && pair.Value.Equals(tag, StringComparison.OrdinalIgnoreCase))
			{
				RemoveTag(pair.Key);
			}
		}

		// If we just removed the SRKW tag and the radio button says
		// SRKW=yes, clear that.
		if (tag.Equals("srkw", StringComparison.OrdinalIgnoreCase) && Detection.Found == "Yes")
		{
			SetFoundValue(string.Empty);
		}
	}

    /// <summary>
    /// Process a change in the tags string, updating the Detection's TagList accordingly.
    /// </summary>
    /// <param name="tags">New tags string</param>
    private void OnTagsChanged(string tags)
    {
        var normalizedTags = string.IsNullOrWhiteSpace(tags)
            ? new List<string>()
             : Detection.GetTagList(tags)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var existingTags = Detection.TagList.ToList();
        var tagsToRemove = existingTags
            .Where(tag => !normalizedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var tag in tagsToRemove)
        {
            RemoveTag(tag);
        }

        var tagsToAdd = normalizedTags
            .Where(tag => !existingTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .ToList();
        foreach (var tag in tagsToAdd)
        {
            AddTag(tag);
        }

        Detection.Tags = string.Join(";", Detection.TagList);
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

	private string RegionsJson =>
		JsonSerializer.Serialize(Detection.Annotations.Select(annotation => new
		{
			start = annotation.StartTime,
			end = annotation.EndTime,
			color = "rgba(255, 255, 255, 0.1)"
		}));

	private async Task InitializeModalPlayer()
	{
		await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
		await JSRuntime.InvokeVoidAsync("InitializeModalSpectrogram", _id,
			Detection.AudioUri);
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
