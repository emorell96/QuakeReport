using System.Text.RegularExpressions;
using FluentValidation;

namespace QuakeReport.Web.Models;

public abstract class MudFormValidator<T> : AbstractValidator<T> where T : class
{
    public Func<object, string, Task<IEnumerable<string>>> ValidateValue =>
        (model, propertyName) => ValidatePropertyAsync((T)model, propertyName);

    public async Task<IEnumerable<string>> ValidatePropertyAsync(T model, string propertyName)
    {
        var context = ValidationContext<T>.CreateWithOptions(
            model,
            options => options.IncludeProperties(propertyName));
        var result = await ValidateAsync(context);
        return result.IsValid ? [] : result.Errors.Select(error => error.ErrorMessage);
    }
}

public static partial class FormValidationExtensions
{
    public static void AddTurnstileRules<T>(this AbstractValidator<T> validator) where T : ITurnstileProtectedForm =>
        validator.RuleFor(form => form.TurnstileToken)
            .NotEmpty().WithMessage("Completa la verificación humana.");

    public static void AddPrivacyConsentRules<T>(this AbstractValidator<T> validator) where T : IPrivacyConsentForm =>
        validator.RuleFor(form => form.PrivacyConsent)
            .Equal(true).WithMessage("Debes aceptar la política de tratamiento de datos.");

    public static void AddOrganizationRules<T>(this AbstractValidator<T> validator) where T : IFormWithOrganization =>
        validator.RuleFor(form => form.OrganizationName)
            .MaximumLength(200).WithMessage("La organización no puede superar 200 caracteres.");

    public static void AddEmailRules<T>(this AbstractValidator<T> validator) where T : IFormWithEmail
    {
        validator.RuleFor(form => form.Email)
            .MaximumLength(320).WithMessage("El correo no puede superar 320 caracteres.")
            .EmailAddress().When(form => !string.IsNullOrWhiteSpace(form.Email))
            .WithMessage("Ingresa un correo electrónico válido.");
    }

    public static void AddPhoneRules<T>(this AbstractValidator<T> validator) where T : IFormWithPhone =>
        validator.RuleFor(form => form.Phone)
            .MaximumLength(80).WithMessage("El teléfono no puede superar 80 caracteres.")
            .Must(BeAValidPhone).When(form => !string.IsNullOrWhiteSpace(form.Phone))
            .WithMessage("Ingresa un teléfono válido con entre 7 y 20 dígitos.");

    public static void AddWhatsAppRules<T>(this AbstractValidator<T> validator) where T : IFormWithWhatsApp =>
        validator.RuleFor(form => form.WhatsApp)
            .MaximumLength(80).WithMessage("El WhatsApp no puede superar 80 caracteres.")
            .Must(BeAValidPhone).When(form => !string.IsNullOrWhiteSpace(form.WhatsApp))
            .WithMessage("Ingresa un WhatsApp válido con entre 7 y 20 dígitos.");

    public static void RequirePhoneOrWhatsApp<T>(this AbstractValidator<T> validator)
        where T : IFormWithPhone, IFormWithWhatsApp =>
        validator.RuleFor(form => form)
            .Must(form => !string.IsNullOrWhiteSpace(form.Phone) || !string.IsNullOrWhiteSpace(form.WhatsApp))
            .WithName(nameof(IFormWithPhone.Phone))
            .WithMessage("Indica un teléfono o WhatsApp público.");

    public static void AddLocationRules<T>(this AbstractValidator<T> validator, int addressMaxLength,
        bool addressRequired = true, bool coordinatesRequired = false) where T : IFormWithLocation
    {
        var addressRule = validator.RuleFor(form => form.Address)
            .MaximumLength(addressMaxLength)
            .WithMessage($"La dirección no puede superar {addressMaxLength} caracteres.");
        if (addressRequired)
            addressRule.NotEmpty().WithMessage("La dirección es obligatoria.");

        validator.RuleFor(form => form.Latitude)
            .NotNull().When(form => coordinatesRequired || form.Longitude.HasValue, ApplyConditionTo.CurrentValidator)
            .WithMessage("La latitud y longitud deben indicarse juntas.")
            .InclusiveBetween(-90, 90).When(form => form.Latitude.HasValue, ApplyConditionTo.CurrentValidator)
            .WithMessage("La latitud debe estar entre -90 y 90.");
        validator.RuleFor(form => form.Longitude)
            .NotNull().When(form => coordinatesRequired || form.Latitude.HasValue, ApplyConditionTo.CurrentValidator)
            .WithMessage("La latitud y longitud deben indicarse juntas.")
            .InclusiveBetween(-180, 180).When(form => form.Longitude.HasValue, ApplyConditionTo.CurrentValidator)
            .WithMessage("La longitud debe estar entre -180 y 180.");
    }

    private static bool BeAValidPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !PhoneCharacters().IsMatch(value)) return false;
        var digits = value.Count(char.IsDigit);
        return digits is >= 7 and <= 20;
    }

    [GeneratedRegex(@"^[0-9+\s().-]+$")]
    private static partial Regex PhoneCharacters();
}
