using System.Diagnostics;

namespace OrcaHello.Web.UI.Pages.Detections.Components
{
    [ExcludeFromCodeCoverage]
    public partial class EditableModeratorFieldsComponent
    {
        [Inject]
        public IDetectionViewService ViewService { get; set; } = null!;

        [Parameter]
        public DetectionItemView DetectionItemView { get; set; } = new();

        [Parameter]
        public List<string> AvailableTags { get; set; } = new();

        [Parameter]
        public string Moderator { get; set; } = string.Empty;

        [Parameter]
        public EventCallback ReloadCurrentView { get; set; }

        protected string StateValidationMessage = string.Empty; // Error message holder

        protected void OnEnteredTagsChanged()
        {
            if (string.IsNullOrWhiteSpace(DetectionItemView?.Tags))
            {
                return;
            }

            var enteredTags = DetectionItemView.Tags.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(s => s.Trim())
                                              .Distinct();

            var newTags = enteredTags.Except(AvailableTags);
            AvailableTags.AddRange(newTags);
            AvailableTags.Sort();
        }

        protected async Task OnSubmitClicked()
        {
            if (string.IsNullOrEmpty(DetectionItemView.State) || DetectionItemView.State == DetectionState.Unreviewed.ToString())
            {
                StateValidationMessage = "You must indicate whether a whale call was heard, not heard, or undetermined for the selected candidates.";
            }
            else
            {
                var response = await ViewService.ModerateDetectionsAsync(
                    new List<string> { DetectionItemView.Id },
                    DetectionItemView.State,
                    Moderator,
                    DetectionItemView.Comments,
                    DetectionItemView.Tags);

                if (response.IdsToUpdate.Count == response.IdsSuccessfullyUpdated.Count)
                {
                    ReportSuccess("Success",
                        "Record successfully updated (and may disappear from this list).");
                }
                else
                {
                    ReportError("Failure",
                        "Record was not updated.");
                }

                await ReloadCurrentView.InvokeAsync();
            }
        }
    }
}
