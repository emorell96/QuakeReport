using QuakeReport.Web.Services;

namespace QuakeReport.Web.Helpers;

public static class GeolocationMessages
{
    public static string For(GeolocationFailureReason reason) =>
        reason switch
        {
            GeolocationFailureReason.Denied =>
                "Necesitamos permiso para acceder a tu ubicación.",
            GeolocationFailureReason.Timeout =>
                "No pudimos obtener tu ubicación a tiempo. Inténtalo de nuevo.",
            GeolocationFailureReason.Unsupported =>
                "Tu navegador no admite servicios de ubicación.",
            _ => "Tu ubicación no está disponible en este momento.",
        };
}
