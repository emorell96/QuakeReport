using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuakeReport.Contracts.Enums;
using QuakeReport.Core.Models.API;

namespace QuakeReport.ApiService.Validation;

public sealed class PaginationRequestValidator : AbstractValidator<PaginationRequest>
{
    public PaginationRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100);
    }
}

public sealed class GeoPointQueryValidator : AbstractValidator<GeoPointQuery>
{
    public GeoPointQueryValidator()
    {
        RuleFor(query => query.Latitude)
            .InclusiveBetween(-90, 90);

        RuleFor(query => query.Longitude)
            .InclusiveBetween(-180, 180);
    }
}

public abstract class PagedSearchRequestValidator<TFilter> : AbstractValidator<PagedRequest<TFilter>>
    where TFilter : class
{
    protected PagedSearchRequestValidator(IValidator<TFilter> filterValidator)
    {
        RuleFor(request => request.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(request => request.Filter)
            .NotNull();

        RuleFor(request => request.Filter!)
            .SetValidator(filterValidator)
            .When(request => request.Filter is not null);
    }
}

public sealed class BloodDonationCenterSearchFilterValidator
    : AbstractValidator<BloodDonationCenterSearchFilter>
{
    private const BloodTypeFlags ValidBloodTypes =
        BloodTypeFlags.APositive |
        BloodTypeFlags.ANegative |
        BloodTypeFlags.BPositive |
        BloodTypeFlags.BNegative |
        BloodTypeFlags.ABPositive |
        BloodTypeFlags.ABNegative |
        BloodTypeFlags.OPositive |
        BloodTypeFlags.ONegative |
        BloodTypeFlags.Unknown;

    private const BloodComponentFlags ValidComponents =
        BloodComponentFlags.WholeBlood |
        BloodComponentFlags.RedBloodCells |
        BloodComponentFlags.Plasma |
        BloodComponentFlags.Platelets |
        BloodComponentFlags.Unknown;

    public BloodDonationCenterSearchFilterValidator(IValidator<GeoPointQuery> geoPointValidator)
    {
        RuleFor(filter => filter.Sort)
            .IsInEnum();

        RuleFor(filter => filter.CenterType)
            .Must(IsDefined);

        RuleFor(filter => filter.OperationalStatus)
            .Must(IsDefined);

        RuleFor(filter => filter.ModerationStatus)
            .Must(IsDefined);

        RuleFor(filter => filter.BloodTypes)
            .Must(value => value is null || IsValidFlags(value.Value, ValidBloodTypes));

        RuleFor(filter => filter.Components)
            .Must(value => value is null || IsValidFlags(value.Value, ValidComponents));

        RuleFor(filter => filter.CenterPoint!)
            .SetValidator(geoPointValidator)
            .When(filter => filter.CenterPoint is not null);
    }

    private static bool IsDefined<TEnum>(TEnum? value)
        where TEnum : struct, Enum =>
        value is null || Enum.IsDefined(value.Value);

    private static bool IsValidFlags<TEnum>(TEnum value, TEnum validValues)
        where TEnum : struct, Enum
    {
        var numericValue = Convert.ToInt64(value);
        var numericValidValues = Convert.ToInt64(validValues);
        return numericValue > 0 && (numericValue & ~numericValidValues) == 0;
    }
}

public sealed class CollectionPointSearchFilterValidator
    : AbstractValidator<CollectionPointSearchFilter>
{
    public CollectionPointSearchFilterValidator(IValidator<GeoPointQuery> geoPointValidator)
    {
        RuleFor(filter => filter.Sort)
            .IsInEnum();

        RuleFor(filter => filter.OperationalStatus)
            .Must(IsDefined);

        RuleFor(filter => filter.ModerationStatus)
            .Must(IsDefined);

        RuleFor(filter => filter.CenterPoint!)
            .SetValidator(geoPointValidator)
            .When(filter => filter.CenterPoint is not null);
    }

    private static bool IsDefined<TEnum>(TEnum? value)
        where TEnum : struct, Enum =>
        value is null || Enum.IsDefined(value.Value);
}

public sealed class ShelterSearchFilterValidator : AbstractValidator<ShelterSearchFilter>
{
    public ShelterSearchFilterValidator(IValidator<GeoPointQuery> geoPointValidator)
    {
        RuleFor(filter => filter.Sort)
            .IsInEnum();

        RuleFor(filter => filter.OperationalStatus)
            .Must(IsDefined);

        RuleFor(filter => filter.ModerationStatus)
            .Must(IsDefined);

        RuleFor(filter => filter.CenterPoint!)
            .SetValidator(geoPointValidator)
            .When(filter => filter.CenterPoint is not null);
    }

    private static bool IsDefined<TEnum>(TEnum? value)
        where TEnum : struct, Enum =>
        value is null || Enum.IsDefined(value.Value);
}

public sealed class HelpRequestSearchFilterValidator : AbstractValidator<HelpRequestSearchFilter>
{
    private const HelpNeedCategory ValidCategories =
        HelpNeedCategory.Personnel |
        HelpNeedCategory.Medical |
        HelpNeedCategory.RescueEquipment |
        HelpNeedCategory.Machinery |
        HelpNeedCategory.Transportation |
        HelpNeedCategory.FoodAndWater |
        HelpNeedCategory.Communications |
        HelpNeedCategory.TemporaryShelter |
        HelpNeedCategory.Security |
        HelpNeedCategory.Other;

    public HelpRequestSearchFilterValidator(IValidator<GeoPointQuery> geoPointValidator)
    {
        RuleFor(filter => filter.Sort)
            .IsInEnum();

        RuleFor(filter => filter.Priority)
            .Must(IsDefined);

        RuleFor(filter => filter.Status)
            .Must(IsDefined);

        RuleFor(filter => filter.ModerationStatus)
            .Must(IsDefined);

        RuleFor(filter => filter.Category)
            .Must(value => value is null || IsValidCategories(value.Value));

        RuleFor(filter => filter.CenterPoint!)
            .SetValidator(geoPointValidator)
            .When(filter => filter.CenterPoint is not null);
    }

    private static bool IsDefined<TEnum>(TEnum? value)
        where TEnum : struct, Enum =>
        value is null || Enum.IsDefined(value.Value);

    private static bool IsValidCategories(HelpNeedCategory value) =>
        value != 0 && (value & ~ValidCategories) == 0;
}

public sealed class MissingPersonSearchFilterValidator : AbstractValidator<MissingPersonSearchFilter>
{
    public MissingPersonSearchFilterValidator()
    {
        RuleFor(filter => filter.Sort)
            .IsInEnum();

        RuleFor(filter => filter.Status)
            .Must(value => value is null || Enum.IsDefined(value.Value));
    }
}

public sealed class DamageReportSearchFilterValidator : AbstractValidator<DamageReportSearchFilter>
{
    public DamageReportSearchFilterValidator()
    {
        RuleFor(filter => filter.Sort)
            .IsInEnum();

        RuleFor(filter => filter.Severity)
            .Must(value => value is null || Enum.IsDefined(value.Value));
    }
}

public sealed class BloodDonationCenterSearchRequestValidator(
    IValidator<BloodDonationCenterSearchFilter> filterValidator)
    : PagedSearchRequestValidator<BloodDonationCenterSearchFilter>(filterValidator);

public sealed class CollectionPointSearchRequestValidator(
    IValidator<CollectionPointSearchFilter> filterValidator)
    : PagedSearchRequestValidator<CollectionPointSearchFilter>(filterValidator);

public sealed class ShelterSearchRequestValidator(
    IValidator<ShelterSearchFilter> filterValidator)
    : PagedSearchRequestValidator<ShelterSearchFilter>(filterValidator);

public sealed class HelpRequestSearchRequestValidator(
    IValidator<HelpRequestSearchFilter> filterValidator)
    : PagedSearchRequestValidator<HelpRequestSearchFilter>(filterValidator);

public sealed class MissingPersonSearchRequestValidator(
    IValidator<MissingPersonSearchFilter> filterValidator)
    : PagedSearchRequestValidator<MissingPersonSearchFilter>(filterValidator);

public sealed class DamageReportSearchRequestValidator(
    IValidator<DamageReportSearchFilter> filterValidator)
    : PagedSearchRequestValidator<DamageReportSearchFilter>(filterValidator);

public static class ValidationResultExtensions
{
    public static ValidationProblemDetails ToProblemDetails(
        this ValidationResult validationResult,
        string title)
    {
        return new ValidationProblemDetails(validationResult.ToDictionary())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
        };
    }
}
