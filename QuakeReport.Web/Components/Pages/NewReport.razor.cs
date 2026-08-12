using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using QuakeReport.Contracts.Enums;
using QuakeReport.Web.Models;
using QuakeReport.Web.Services;

namespace QuakeReport.Web.Components.Pages;

public partial class NewReport
{
    private enum LocationState { Loading, Success, Failed }

    private MudForm? _form;
    private readonly DamageReportForm _model = new();
    private readonly DamageReportFormValidator _validator = new();
    private LocationState _locationState = LocationState.Loading;
    private string? _locationErrorMessage;
    private bool _submitting;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RequestLocationAsync();
        }
    }

    private async Task RequestLocationAsync()
    {
        _locationState = LocationState.Loading;
        StateHasChanged();

        var result = await Geolocation.GetCurrentPositionAsync();
        switch (result)
        {
            case GeolocationResult.Success success:
                _model.Latitude = success.Latitude;
                _model.Longitude = success.Longitude;
                _locationState = LocationState.Success;
                await TryPrefillAddressAsync(success.Latitude, success.Longitude);
                break;
            case GeolocationResult.Failure failure:
                _locationErrorMessage = failure.Reason switch
                {
                    GeolocationFailureReason.Denied => "Se denegó el acceso a la ubicación.",
                    GeolocationFailureReason.Timeout => "Se agotó el tiempo para obtener tu ubicación; revisa los permisos del navegador.",
                    GeolocationFailureReason.Unsupported => "Tu navegador no admite servicios de ubicación.",
                    _ => "La ubicación no está disponible en este momento.",
                };
                _locationState = LocationState.Failed;
                break;
        }

        StateHasChanged();
    }

    private async Task TryPrefillAddressAsync(double latitude, double longitude)
    {
        if (!string.IsNullOrWhiteSpace(_model.Address))
        {
            return;
        }

        try
        {
            _model.Address = await GooglePlaces.ReverseGeocodeAsync(latitude, longitude);
        }
        catch
        {
            // Reverse geocoding is a convenience and never blocks submission.
        }
    }

    private void OnPlaceSelected((string Address, double Latitude, double Longitude) place)
    {
        _model.Address = place.Address;
        _model.Latitude = place.Latitude;
        _model.Longitude = place.Longitude;
        _locationState = LocationState.Success;
    }

    private void ClearCoordinates()
    {
        _model.Latitude = null;
        _model.Longitude = null;
        _locationErrorMessage = "Selecciona una dirección o vuelve a obtener tu ubicación.";
        _locationState = LocationState.Failed;
    }

    private void ToggleSign(DamageSign sign, bool selected) =>
        _model.DamageSigns = selected ? _model.DamageSigns | sign : _model.DamageSigns & ~sign;

    private void OnFilesSelected(InputFileChangeEventArgs args)
    {
        foreach (var file in args.GetMultipleFiles(100))
        {
            _model.MediaFiles.Add(file);
        }
    }

    private async Task SubmitAsync()
    {
        await _form!.ValidateAsync();
        var validation = await _validator.ValidateAsync(_model);
        if (!_form.IsValid || !validation.IsValid)
        {
            Snackbar.Add(string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct()), Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            var report = await ApiClient.CreateReportAsync(_model.ToApiDto());
            foreach (var file in _model.MediaFiles)
            {
                var mediaType = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                    ? MediaType.Video
                    : MediaType.Photo;
                await using var stream = file.OpenReadStream(DamageReportFormValidator.MaxFileSizeBytes);
                await ApiClient.UploadMediaAsync(report.Id, file.Name, file.ContentType, mediaType, stream);
            }

            Snackbar.Add("Reporte enviado.", Severity.Success);
            Navigation.NavigateTo($"/report/{report.Id}");
        }
        catch (Exception exception)
        {
            Snackbar.Add($"No se pudo enviar el reporte: {exception.Message}", Severity.Error);
        }
        finally
        {
            _submitting = false;
        }
    }
}
