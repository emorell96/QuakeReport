using QuakeReport.Contracts.Enums;

namespace QuakeReport.ApiService.Dtos;

public class UploadReportMediaRequest
{
    public required IFormFile File { get; set; }

    public required MediaType MediaType { get; set; }
}
