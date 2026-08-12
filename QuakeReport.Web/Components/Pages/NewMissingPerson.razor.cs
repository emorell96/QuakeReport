using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using QuakeReport.Web.Models;

namespace QuakeReport.Web.Components.Pages;

public partial class NewMissingPerson
{
    private MudForm? _form;
    private readonly MissingPersonForm _model = new();
    private readonly MissingPersonFormValidator _validator = new();
    private readonly MissingPersonLocationFormValidator _locationValidator = new();
    private string? _managementCode;
    private string? _error;
    private string? _photoError;
    private Guid? _createdId;
    private bool _saving;

    private void AddLocation() => _model.Locations.Add(new MissingPersonLocationForm());
    private void RemoveLocation(int index) => _model.Locations.RemoveAt(index);

    private static void SetLocation(
        MissingPersonLocationForm location,
        (string Address, double Latitude, double Longitude) details)
    {
        location.Address = details.Address;
        location.Latitude = details.Latitude;
        location.Longitude = details.Longitude;
    }

    private static void ClearCoordinates(MissingPersonLocationForm location)
    {
        location.Latitude = null;
        location.Longitude = null;
    }

    private void OnPhotoSelected(InputFileChangeEventArgs args)
    {
        _photoError = null;
        _model.Photo = args.File;
        if (_model.Photo.Size > 10 * 1024 * 1024)
        {
            _photoError = "La fotografía no puede superar 10 MB.";
        }
        else if (_model.Photo.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            _photoError = "Selecciona una imagen JPEG, PNG o WebP.";
        }
    }

    private async Task SubmitAsync()
    {
        _error = null;
        await _form!.ValidateAsync();
        var validation = await _validator.ValidateAsync(_model);
        if (!_form.IsValid || !validation.IsValid)
        {
            _error = string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct());
            return;
        }

        _saving = true;
        try
        {
            var response = await ApiClient.CreateMissingPersonAsync(_model.ToApiDto());
            _managementCode = response.ManagementCode;
            _createdId = response.Person.Id;

            if (_model.Photo is not null)
            {
                try
                {
                    await ApiClient.UploadMissingPersonPhotoAsync(response.Person.Id, response.ManagementCode, _model.Photo);
                }
                catch (Exception exception)
                {
                    _photoError = $"El registro se creó, pero la fotografía no pudo cargarse: {exception.Message}";
                }
            }
        }
        catch (Exception exception)
        {
            _error = exception.Message;
        }
        finally
        {
            _saving = false;
        }
    }
}
