using QuakeReport.ApiService.Validation;
using QuakeReport.Contracts.Enums;
using QuakeReport.Core.Models.API;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class SearchRequestValidatorTests
{
    [TestMethod]
    public async Task GeoPointValidatorAcceptsZeroAndCoordinateBoundaries()
    {
        var validator = new GeoPointQueryValidator();
        var validPoints = new[]
        {
            new GeoPointQuery { Latitude = 0, Longitude = 0 },
            new GeoPointQuery { Latitude = -90, Longitude = -180 },
            new GeoPointQuery { Latitude = 90, Longitude = 180 },
        };

        foreach (var point in validPoints)
        {
            var result = await validator.ValidateAsync(point);
            Assert.IsTrue(result.IsValid);
        }
    }

    [TestMethod]
    public async Task GeoPointValidatorRejectsCoordinatesOutsideBoundaries()
    {
        var validator = new GeoPointQueryValidator();
        var invalidPoints = new[]
        {
            new GeoPointQuery { Latitude = -90.01, Longitude = 0 },
            new GeoPointQuery { Latitude = 90.01, Longitude = 0 },
            new GeoPointQuery { Latitude = 0, Longitude = -180.01 },
            new GeoPointQuery { Latitude = 0, Longitude = 180.01 },
        };

        foreach (var point in invalidPoints)
        {
            var result = await validator.ValidateAsync(point);
            Assert.IsFalse(result.IsValid);
        }
    }

    [TestMethod]
    public async Task PagedSearchValidatorRejectsInvalidPaginationAndNullFilter()
    {
        var filterValidator = new DamageReportSearchFilterValidator();
        var validator = new DamageReportSearchRequestValidator(filterValidator);
        var request = new PagedRequest<DamageReportSearchFilter>
        {
            PageNumber = 0,
            PageSize = 101,
            Filter = null,
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsTrue(result.Errors.Any(error => error.PropertyName == "PageNumber"));
        Assert.IsTrue(result.Errors.Any(error => error.PropertyName == "PageSize"));
        Assert.IsTrue(result.Errors.Any(error => error.PropertyName == "Filter"));
    }

    [TestMethod]
    public async Task LocationAwareValidatorsUseTheSharedGeoPointRules()
    {
        var invalidPoint = new GeoPointQuery
        {
            Latitude = 91,
            Longitude = 181,
        };
        var geoPointValidator = new GeoPointQueryValidator();

        var bloodResult = await new BloodDonationCenterSearchFilterValidator(geoPointValidator)
            .ValidateAsync(new BloodDonationCenterSearchFilter { CenterPoint = invalidPoint });
        var collectionPointResult = await new CollectionPointSearchFilterValidator(geoPointValidator)
            .ValidateAsync(new CollectionPointSearchFilter { CenterPoint = invalidPoint });
        var shelterResult = await new ShelterSearchFilterValidator(geoPointValidator)
            .ValidateAsync(new ShelterSearchFilter { CenterPoint = invalidPoint });

        Assert.IsTrue(bloodResult.Errors.Any(error => error.PropertyName == "CenterPoint.Latitude"));
        Assert.IsTrue(collectionPointResult.Errors.Any(error => error.PropertyName == "CenterPoint.Latitude"));
        Assert.IsTrue(shelterResult.Errors.Any(error => error.PropertyName == "CenterPoint.Latitude"));
    }

    [TestMethod]
    public async Task BloodDonationValidatorRejectsUnknownEnumsAndFlags()
    {
        var validator = new BloodDonationCenterSearchFilterValidator(
            new GeoPointQueryValidator());
        var filter = new BloodDonationCenterSearchFilter
        {
            Sort = (BloodDonationSortOption)999,
            CenterType = (BloodDonationCenterType)999,
            BloodTypes = (BloodTypeFlags)(1 << 20),
            Components = BloodComponentFlags.None,
        };

        var result = await validator.ValidateAsync(filter);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.PropertyName == "Sort"));
        Assert.IsTrue(result.Errors.Any(error => error.PropertyName == "CenterType"));
        Assert.IsTrue(result.Errors.Any(error => error.PropertyName == "BloodTypes"));
        Assert.IsTrue(result.Errors.Any(error => error.PropertyName == "Components"));
    }
}
