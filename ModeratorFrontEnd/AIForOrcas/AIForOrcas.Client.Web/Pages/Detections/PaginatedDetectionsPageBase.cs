using AIForOrcas.Client.BL.Services;
using AIForOrcas.Client.Web.Models;
using AIForOrcas.DTO;
using AIForOrcas.DTO.API;
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIForOrcas.Client.Web.Pages.Detections
{
    public abstract class PaginatedDetectionsPageBase<TFilter> : ComponentBase, IDisposable
        where TFilter : IFilterOptions, new()
    {
        [Inject]
        protected IJSRuntime JSRuntime { get; set; }

        [Inject]
        protected IDetectionService Service { get; set; }

        [Inject]
        protected IToastService ToastService { get; set; }

        [Inject]
        protected UserTagCache TagCache { get; set; }

        [Inject]
        protected AuthenticationStateProvider AuthenticationStateProvider { get; set; }

        protected string _userId;
        protected List<Detection> detections = null;
        protected List<DetectionMinute> detectionMinutes = null;

        protected PaginationOptionsDTO paginationOptions = new PaginationOptionsDTO() { RecordsPerPage = 0, MinutesPerPage = 5, Page = 1 };

        protected TFilter filterOptions = new TFilter();

        protected PaginationResultsDTO pagination = new PaginationResultsDTO();

        protected string loadStatus = null;

        // Set after a submit; consumed once the re-rendered list is in the DOM.
        protected string _scrollToDetectionId;

        protected override async Task OnInitializedAsync()
        {
            await LoadDetections();

            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            _userId = user.FindFirst("oid")?.Value;
        }

        protected virtual async Task LoadDetections()
        {
            loadStatus = "Loading records...";
            detections = null;

            var paginatedResponse = await FetchDetectionsAsync(paginationOptions, filterOptions);

            pagination.TotalNumberOfRecords = paginatedResponse.TotalNumberRecords;
            pagination.TotalNumberOfMinutes = paginatedResponse.TotalNumberMinutes;
            pagination.TotalNumberOfPages = paginatedResponse.TotalAmountPages;

            // The page we requested may no longer exist.
            // Clamp to the actual last valid page. re-fetch to render real data
            if (pagination.TotalNumberOfPages > 0 && paginationOptions.Page > pagination.TotalNumberOfPages)
            {
                paginationOptions.Page = pagination.TotalNumberOfPages;
                paginatedResponse = await FetchDetectionsAsync(paginationOptions, filterOptions);
                pagination.TotalNumberOfRecords = paginatedResponse.TotalNumberRecords;
                pagination.TotalNumberOfMinutes = paginatedResponse.TotalNumberMinutes;
                pagination.TotalNumberOfPages = paginatedResponse.TotalAmountPages;
            }

            pagination.CurrentPage = paginationOptions.Page;

            if (paginatedResponse.Response == null)
            {
                loadStatus = "An unknown error occurred while loading records...";
            }
            else if (paginatedResponse.Response.Count == 0)
            {
                // Default empty message; pages can override by overriding SetEmptyLoadStatus
                SetEmptyLoadStatus(paginatedResponse);
            }
            else
            {
                loadStatus = null;
                detections = paginatedResponse.Response;
            }

            detectionMinutes = DetectionMinute.CreateDetectionMinutes(detections);
        }

        protected virtual void SetEmptyLoadStatus(PaginatedResponseDTO<List<Detection>> paginatedResponse)
        {
            loadStatus = "No records found for the selected filter options. Please select a different set of filter options...";
        }

        protected abstract Task<PaginatedResponseDTO<List<Detection>>> FetchDetectionsAsync(PaginationOptionsDTO paginationOptions, TFilter filterOptions);

        protected virtual async Task ActOnSelectPageCallback(PaginationOptionsDTO returnedPaginationOptions)
        {
            paginationOptions = returnedPaginationOptions;
            await LoadDetections();
            await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
            StateHasChanged();
        }

        protected virtual async Task ActOnApplyFilterCallback(TFilter returnedFilterOptions)
        {
            filterOptions = returnedFilterOptions;
            paginationOptions.Page = 1;
            await LoadDetections();
            await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
            StateHasChanged();
        }

        protected virtual async Task ActOnSubmitCallback(DetectionUpdate request)
        {
            await Service.UpdateRequestAsync(request);

            List<string> leafTags = Detection.GetLeafTags(request.Tags);
            TagCache.SetTags(_userId, leafTags);

            await LoadDetections();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (_scrollToDetectionId != null)
            {
                string detectionId = _scrollToDetectionId;
                _scrollToDetectionId = null;
                await JSRuntime.InvokeVoidAsync("ScrollCardIntoView", detectionId);
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        public void Dispose()
        {
            JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
        }
    }
}
