using MudBlazor;
using QuakeReport.Contracts.Enums;
using QuakeReport.Web.Models;

namespace QuakeReport.Web.Components.Pages;

public partial class NewBloodDonationCenter
{
    private sealed record Choice<T>(T Value, string Label);

    private readonly Choice<BloodTypeFlags>[] BloodTypes =
    [
        new(BloodTypeFlags.APositive, "A+"), new(BloodTypeFlags.ANegative, "A−"),
        new(BloodTypeFlags.BPositive, "B+"), new(BloodTypeFlags.BNegative, "B−"),
        new(BloodTypeFlags.ABPositive, "AB+"), new(BloodTypeFlags.ABNegative, "AB−"),
        new(BloodTypeFlags.OPositive, "O+"), new(BloodTypeFlags.ONegative, "O−"),
        new(BloodTypeFlags.Unknown, "No sé"),
    ];

    private readonly Choice<BloodComponentFlags>[] Components =
    [
        new(BloodComponentFlags.WholeBlood, "Sangre total"),
        new(BloodComponentFlags.RedBloodCells, "Glóbulos rojos"),
        new(BloodComponentFlags.Plasma, "Plasma"),
        new(BloodComponentFlags.Platelets, "Plaquetas"),
        new(BloodComponentFlags.Unknown, "No sé"),
    ];

    private MudForm? _form;
    private readonly BloodDonationCenterForm _model = new();
    private readonly BloodDonationCenterFormValidator _validator = new();
    private string? _code;
    private string? _error;
    private bool _saving;

    private void ToggleBlood(BloodTypeFlags value, bool selected) =>
        _model.BloodTypes = selected ? _model.BloodTypes | value : _model.BloodTypes & ~value;

    private void ToggleComponent(BloodComponentFlags value, bool selected) =>
        _model.Components = selected ? _model.Components | value : _model.Components & ~value;

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
            var response = await ApiClient.CreateBloodDonationCenterAsync(_model.ToApiDto());
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
