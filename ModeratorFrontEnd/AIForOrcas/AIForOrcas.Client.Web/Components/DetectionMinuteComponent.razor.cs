using AIForOrcas.Client.BL.Helpers;
using AIForOrcas.Client.Web.Models;
using System.Text.RegularExpressions;

namespace AIForOrcas.Client.Web.Components;

public partial class DetectionMinuteComponent
{
    private string _id;
    private string _userId;
    private DetectionMinute _initializedDetectionMinute;
    private bool _submitting;
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
    IConfiguration Configuration { get; set; }

    [Inject]
    UserTagCache TagCache { get; set; }

    [Inject]
    IToastService ToastService { get; set; }

    [Parameter]
    public DetectionMinute DetectionMinute { get; set; }

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

    private string DetectionCount
    {
        get
        {
            if (DetectionMinute.Annotations.Count == 1)
            {
                return "1 detection";
            }
            string value = string.Empty;
            foreach (var d in DetectionMinute.Detections)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    value += ", ";
                }
                value += $"{d.Annotations.Count}";
            }
            return $"{value} detections";
        }
    }

    private string Confidence
    {
        get
        {
            string value = string.Empty;
            foreach (var d in DetectionMinute.Detections)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    value += ", ";
                }
                value += $"{d.Confidence.ToString("00.##")}%";
            }
            return $"{value} average confidence";
        }
    }

    private bool IsSubmitDisabled { get => _submitting || string.IsNullOrWhiteSpace(DetectionMinute.Found); }

    private string WasFound { get => _ti.ToTitleCase(DetectionMinute.Found); }

    // The minute's Moderator joins each reviewer's identity with a comma;
    // ExtractName only handles a single identity, so extract per identity
    // before joining, or a two-moderator minute would show only the first name.
    private string ModeratorNames
    {
        get => string.Join(", ", (DetectionMinute.Moderator ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(identity => EmailHelper.ExtractName(identity.Trim())));
    }

    private string LinkUrl { get => $"{NavigationManager.BaseUri}detections/detection/{DetectionMinute.Id}"; }

    public List<string> GetSuggestedTagList(DetectionMinute dm)
    {
        var suggestedTags = new List<string>();

        // Add any tags not in TagList that were leaf tags in the most recently moderated detection.
        foreach (var tag in TagCache.GetTags(_userId))
        {
            if (!dm.TagList.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                suggestedTags.Add(tag);
            }
        }

        foreach (var tag in dm.SuggestedTagList)
        {
            if (!suggestedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                suggestedTags.Add(tag);
            }
        }

        // Add any default tag suggestions from the DEFAULT_TAG_SUGGESTIONS environment variable.
        var defaultTagSuggestions = Configuration["DEFAULT_TAG_SUGGESTIONS"];
        if (!string.IsNullOrWhiteSpace(defaultTagSuggestions))
        {
            foreach (var tag in defaultTagSuggestions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!dm.TagList.Contains(tag, StringComparer.OrdinalIgnoreCase) &&
                    !suggestedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    suggestedTags.Add(tag);
                }
            }
        }

        return suggestedTags;
    }

    protected override async Task OnParametersSetAsync()
    {
        _id = DetectionMinute.Id;

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _userId = user.FindFirst("oid")?.Value;

        // Unreviewed detections are being initially populated in the database as "No"
        // I am manually resetting it here when the reviewed status is false so that the record,
        // can be unsubmittable until the user has changed Found to "Yes", "No", or "Don't Know"

        // TODO: Determine whether or not we should change the initial Found state
        //       from No to something other than the three options we give the user

        // Only initialize each detection once. This hook runs again on every
        // parent re-render with the same Detection instance, and resetting
        // then would wipe a verdict the moderator already selected (e.g., right
        // after a failed submit shows its retry toast).
        if (!DetectionMinute.Reviewed && !ReferenceEquals(DetectionMinute, _initializedDetectionMinute))
        {
            _initializedDetectionMinute = DetectionMinute;

            // A partially reviewed minute keeps the reviewed detection's verdict
            // visible, so the moderator can see what a submit would overwrite.
            // A fully unreviewed minute still starts unselected.
            DetectionMinute.Found =
                DetectionMinute.Detections?.FirstOrDefault(d => d.Reviewed)?.Found ?? string.Empty;

            if (string.IsNullOrEmpty(DetectionMinute.Tags))
            {
                // Check every model's label, not just the first detection's:
                // a model with an empty label sorting first must not hide
                // another model's transient or humpback prediction.
                foreach (var label in DetectionMinute.GlobalPredictionLabels)
                {
                    if (label == "transient")
                    {
                        AddTag("transient");
                    }
                    else if (label == "humpback")
                    {
                        AddTag("humpback");
                    }
                }

                // Don't add the "srkw" tag here because we want the user
                // to explicitly select it if they see it in the audio.
            }

            // If Comments is of the form "AI: A and B", then parse out the B and add it too.
            if (!string.IsNullOrEmpty(DetectionMinute.Comments))
            {
                var match = Regex.Match(DetectionMinute.Comments, @"AI:\s*(?<a>.*?)\s*and\s*(?<b>.*)");
                if (match.Success)
                {
                    string b = match.Groups["b"].Value;
                    AddTag(b);
                }
            }

            // Handle model tag aliases / legacy tags.
            // PODS-AI may include "resident"; OrcaHello uses "srkw" only when the moderator explicitly
            // selects SRKW=yes, so strip "resident" during initialization to avoid an extra tag.
            // If we later need to map other model labels (e.g., "transient" -> "biggs"), do it here.
            // This does not block moderators from manually entering "resident" later.
            if (DetectionMinute.TagList.Contains("resident", StringComparer.OrdinalIgnoreCase))
            {
                RemoveTag("resident");

                // Don't add srkw since that would make the SRKWFound
                // button default to yes.  Instead leave it unselected,
                // like OrcaHello candidates do.
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Invoked on every render because the card may not be in the DOM yet on the
        // first render (e.g. while the single detection page is still loading the record);
        // the JS side is idempotent and exits early once the shades exist.
        await JSRuntime.InvokeVoidAsync("DrawRegionShades", _id, DetectionMinute.AudioUri, RegionsJson);

        // Once per card is enough: the JS wires its listeners on the first call and
        // re-evaluates every card on each scroll and resize after that.
        if (firstRender)
        {
            await JSRuntime.InvokeVoidAsync("WatchCardLayout");
        }
    }

    private void SetFoundValue(string found)
    {
        DetectionMinute.Found = found;

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
        var tagList = DetectionMinute.TagList;
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
        DetectionMinute.Tags = string.Join(";", tagList);

        // Add parent tag if not already present.
        if (!string.IsNullOrEmpty(parentTag))
        {
            AddTag(parentTag);
        }

        if (tag.Equals("srkw", StringComparison.OrdinalIgnoreCase) && DetectionMinute.Found != "Yes")
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
        var tagList = DetectionMinute.TagList;
        if (!tagList.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            // Nothing to do.
            return;
        }

        tagList.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
        DetectionMinute.Tags = string.Join(";", tagList);

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
        if (tag.Equals("srkw", StringComparison.OrdinalIgnoreCase) && DetectionMinute.Found == "Yes")
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

        var existingTags = DetectionMinute.TagList.ToList();
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

        DetectionMinute.Tags = string.Join(";", DetectionMinute.TagList);
    }

    private async Task SubmitUpdate()
    {
        // Guard before any await: a second click can be dispatched before the
        // disabled attribute reaches the browser, and it must not enter here.
        if (_submitting)
        {
            return;
        }
        _submitting = true;

        var submitted = 0;
        try
        {
            var detections = DetectionMinute.Detections.ToList();
            var comments = DetectionMinute.Comments;
            var tags = DetectionMinute.Tags;
            var found = DetectionMinute.Found;
            var moderator = await AccountService.GetUsername();
            var moderated = DateTime.Now;

            foreach (var d in detections)
            {
                var request = new DetectionUpdate()
                {
                    Id = d.Id,
                    Comments = comments,
                    Tags = tags,
                    Moderator = moderator,
                    Moderated = moderated,
                    Reviewed = true,
                    Found = found
                };

                await SubmitCallback.InvokeAsync(request);
                submitted++;
            }

            ToastService.ShowSuccess("Detection successfully updated.");
        }
        catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException)
        {
            // One guard for every page that renders this component. Keep the
            // card and the moderator's selections untouched for a retry. The
            // wording stays generic: the same exception covers an unreachable
            // server and an error response, and the service logs the detail.
            // Updates are sent one detection at a time, so a failure partway
            // through a minute means the earlier detections did save.
            ToastService.ShowError(submitted == 0
                ? "The verdict was not saved. Please try again."
                : "Only part of the minute was saved. Please submit again to finish.");
        }
        finally
        {
            _submitting = false;
        }
    }

    private async Task ToggleCardPlayer()
    {
        await JSRuntime.InvokeVoidAsync("CardSpectrogram", _id, DetectionMinute.AudioUri);
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

    // Each known model keeps one color on every card, so color means model
    // across the whole queue: OrcaHello keeps the legacy magenta, PODS-AI the
    // Okabe-Ito orange chosen for contrast on the blue spectrogram and
    // separability under color vision deficiency.
    private static readonly Dictionary<string, string> ModelColorRegistry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["OrcaHello"] = "rgba(214, 51, 132, 0.95)",
            ["PODS-AI"] = "rgba(230, 159, 0, 0.95)"
        };

    // Fallback colors for models not in the registry: white, then vermillion
    // D55E00 (also Okabe-Ito).
    private static readonly string[] FallbackColorPalette =
    {
        "rgba(255, 255, 255, 0.95)",
        "rgba(213, 94, 0, 0.95)"
    };

    // Distinct models sorted by name, each paired with its color: registry
    // color when the model is known, else a fallback slot by sorted position,
    // so an unknown model still gets the same color on every card.
    private List<KeyValuePair<string, string>> ModelColors
    {
        get
        {
            var models = DetectionMinute.Detections
                .Where(d => !string.IsNullOrWhiteSpace(d?.AIModel))
                .Select(d => d.AIModel.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var fallbackIndex = 0;
            var pairs = new List<KeyValuePair<string, string>>();
            foreach (var model in models)
            {
                var color = ModelColorRegistry.TryGetValue(model, out var registered)
                    ? registered
                    : FallbackColorPalette[fallbackIndex++ % FallbackColorPalette.Length];
                pairs.Add(new KeyValuePair<string, string>(model, color));
            }
            return pairs;
        }
    }

    private string RegionColorFor(string model)
    {
        var pair = ModelColors.FirstOrDefault(p =>
            p.Key.Equals(model?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (pair.Key != null)
        {
            return pair.Value;
        }

        return ModelColorRegistry.TryGetValue(model?.Trim() ?? string.Empty, out var registered)
            ? registered
            : ModelColorRegistry["OrcaHello"];
    }

    private string RegionsJson =>
        JsonSerializer.Serialize(DetectionMinute.Detections
            .Where(detection => detection?.Annotations != null)
            .SelectMany(detection => detection.Annotations.Select(annotation => new
            {
                start = annotation.StartTime,
                end = annotation.EndTime,
                // Outline only (border in ai-for-orcas.css): a fill all but vanished on a small spectrogram
                color = "rgba(0, 0, 0, 0)",
                borderColor = RegionColorFor(detection.AIModel),
                model = detection.AIModel
            })));

    private async Task InitializeModalPlayer()
    {
        await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
        await JSRuntime.InvokeVoidAsync("InitializeModalSpectrogram", _id,
            DetectionMinute.AudioUri);
    }

    private async Task InitializeModalMap()
    {
        await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
        await JSRuntime.InvokeVoidAsync("LoadBingMap", _id,
            DetectionMinute.Location?.Latitude,
            DetectionMinute.Location?.Longitude);
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
