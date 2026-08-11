using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Services;

public static class HelpRequestDisplay
{
    public static string Priority(HelpRequestPriority value) => value switch { HelpRequestPriority.Low => "Baja", HelpRequestPriority.Medium => "Media", HelpRequestPriority.High => "Alta", _ => "Crítica" };
    public static string Status(HelpRequestStatus value) => value switch { HelpRequestStatus.Active => "Activa", HelpRequestStatus.Assigned => "Asignada", _ => "Resuelta" };
    public static string Category(HelpNeedCategory value) => value switch { HelpNeedCategory.Personnel => "Personal de apoyo", HelpNeedCategory.Medical => "Asistencia o suministros médicos", HelpNeedCategory.RescueEquipment => "Equipos de rescate", HelpNeedCategory.Machinery => "Maquinaria", HelpNeedCategory.Transportation => "Transporte", HelpNeedCategory.FoodAndWater => "Alimentos y agua", HelpNeedCategory.Communications => "Comunicaciones", HelpNeedCategory.TemporaryShelter => "Alojamiento temporal", HelpNeedCategory.Security => "Seguridad", _ => "Otro" };
}
