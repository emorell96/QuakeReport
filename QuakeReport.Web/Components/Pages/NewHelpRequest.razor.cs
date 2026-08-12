using MudBlazor;
using QuakeReport.Web.Models;

namespace QuakeReport.Web.Components.Pages;

public partial class NewHelpRequest
{
    private MudForm? _form;
    private readonly HelpRequestForm _model = new();
    private readonly HelpRequestFormValidator _validator = new();
    private string? _code;
    private string? _error;
    private bool _saving;

    private void OnPlaceSelected((string Address, double Latitude, double Longitude) value)
    {
        _model.Address = value.Address;
        _model.Latitude = value.Latitude;
        _model.Longitude = value.Longitude;
    }

    private void ClearCoordinates()
    {
        _model.Latitude = null;
        _model.Longitude = null;
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
            var response = await ApiClient.CreateHelpRequestAsync(_model.ToApiDto());
            _code = response.ManagementCode;
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
