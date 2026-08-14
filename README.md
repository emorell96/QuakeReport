# QuakeReport

🔗 **Sitio en vivo:** [terremoto.com.co](https://terremoto.com.co)

## Descripción

QuakeReport (Terremoto.com.co) es un proyecto comunitario, gestionado por
voluntarios, para compartir información útil durante la emergencia de un
terremoto. No sustituye a los servicios de emergencia ni a las autoridades
oficiales: es un complemento para que la comunidad se coordine y se ayude
mientras esos canales oficiales se saturan o tardan en llegar. Nace a raíz
del terremoto en Colombia.

La plataforma está abierta a cualquier persona, sin necesidad de crear una
cuenta, y reúne cuatro tipos de información:

- **Reportes de daños** - describe lo ocurrido, la gravedad, el tipo de daño
  (grietas, colapso, incendio, vía bloqueada, personas atrapadas, etc.),
  comparte tu ubicación (por GPS o buscando la dirección) y adjunta fotos o
  videos como evidencia. Los reportes se listan de mayor a menor impacto,
  para ayudar a priorizar la respuesta donde más se necesita.
- **Refugios** - dónde encontrar alojamiento temporal: dirección, estado
  (abierto/cerrado), instrucciones y datos de contacto.
- **Personas desaparecidas** - registra a alguien extraviado con su
  descripción, última ubicación conocida y fotos, y permite que otras
  personas dejen pistas (*tips*) sobre su paradero.
- **Puntos de acopio** - dónde donar o recoger ayuda: qué se necesita, cómo
  entregarlo y hasta cuándo está activo el punto.

### Cómo funciona sin cuentas de usuario

Nadie necesita registrarse para publicar. Al crear un refugio, punto de
acopio o registro de persona desaparecida, se genera un **código de
gestión** (solo vos lo ves) que permite editar o actualizar esa publicación
más adelante sin necesidad de una cuenta. Las publicaciones pasan por un
flujo de **moderación** antes de hacerse públicas, y cualquiera puede
reportar contenido abusivo o incorrecto para que un moderador lo revise.
Un captcha (Cloudflare Turnstile) protege los formularios contra spam.

## Stack tecnológico

- **.NET 10** con **.NET Aspire** para orquestar los servicios en desarrollo
  y para el modelo de despliegue en Azure.
- **Blazor Server** + **MudBlazor** para el sitio web.
- **ASP.NET Core Web API** (controllers) para el backend.
- **PostgreSQL** (Azure Database for PostgreSQL Flexible Server en
  producción) con **Entity Framework Core**.
- **Azure Blob Storage** para fotos y videos adjuntos.
- **Google Maps / Places API** para geolocalización y búsqueda de
  direcciones.
- **Cloudflare Turnstile** para protección contra spam en los formularios
  públicos.
- **Azure Container Apps** como plataforma de despliegue, detrás de
  **Cloudflare** para el dominio `terremoto.com.co`.

## Estructura del proyecto

| Proyecto | Qué contiene |
|---|---|
| `QuakeReport.AppHost` | Modelo de orquestación de Aspire (desarrollo local y despliegue a Azure). |
| `QuakeReport.Web` | Frontend en Blazor Server + MudBlazor. |
| `QuakeReport.ApiService` | API pública (reportes, refugios, personas desaparecidas, puntos de acopio, moderación). |
| `QuakeReport.MigrationService` | Servicio de un solo uso que aplica las migraciones de EF Core al arrancar. |
| `QuakeReport.Data` | Entidades y `DbContext` de EF Core. |
| `QuakeReport.Contracts` | DTOs y enums compartidos entre la API y el frontend. |
| `QuakeReport.ServiceDefaults` | Configuración compartida de Aspire (telemetría, health checks, resiliencia). |
| `QuakeReport.Tests` | Pruebas unitarias e de integración. |

## Cómo ejecutar el proyecto localmente

### Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (para los contenedores de PostgreSQL y el
  emulador de Azure Storage)
- Dos claves de Google Maps Platform:
  - Una clave pública, restringida a los dominios autorizados, para el mapa
    cargado en el navegador.
  - Una clave privada para las solicitudes de Places y Geocoding realizadas
    desde el servidor.

### Pasos

1. Configura los *user secrets* del proyecto `QuakeReport.Web` (clic derecho
   en el proyecto en Visual Studio → *Manage User Secrets*, o desde la
   terminal):

   ```powershell
   dotnet user-secrets set "GOOGLE_MAPS_API_KEY" "<tu-clave>" --project QuakeReport.Web
   dotnet user-secrets set "GOOGLE_MAPS_PRIVATE_API_KEY" "<tu-clave-privada>" --project QuakeReport.Web
   ```

   `GOOGLE_MAPS_API_KEY` se entrega al navegador y debe restringirse por
   referente HTTP a los dominios autorizados. `GOOGLE_MAPS_PRIVATE_API_KEY`
   solo se usa en el servidor para Places y Geocoding; limita esta clave a las
   API necesarias y configura cuotas apropiadas.

2. Ejecuta el `AppHost` (esto levanta todos los servicios, incluyendo los
   contenedores de PostgreSQL y del emulador de Storage, vía Docker):

   ```powershell
   dotnet run --project QuakeReport.AppHost
   ```

3. Abre el *dashboard* de Aspire (la URL aparece en la consola) para ver el
   estado de cada servicio y acceder al sitio web.

### Pruebas

```powershell
dotnet test --filter TestCategory=Unit
```

## Cómo contribuir

Las contribuciones de la comunidad son bienvenidas.

1. Haz un *fork* del repositorio y crea una rama descriptiva a partir de
   `main` (por ejemplo `feature/mejora-formulario-refugios`).
2. Mantén los cambios enfocados: un *pull request* por funcionalidad o
   corrección, sin mezclar temas no relacionados.
3. Sigue el estilo del código ya existente en el archivo que estés
   modificando (nombres, organización de carpetas, patrones ya usados en el
   proyecto).
4. Si agregas o cambias comportamiento en la API o en `QuakeReport.Data`,
   agrega o actualiza las pruebas correspondientes en `QuakeReport.Tests`.
5. Antes de abrir el *pull request*, confirma que el proyecto compila y que
   las pruebas pasan:

   ```powershell
   dotnet build QuakeReport.slnx
   dotnet test --no-build --filter TestCategory=Unit
   ```

6. Describe en el *pull request* qué problema resuelve el cambio y cómo lo
   probaste. Si el cambio afecta la interfaz visible para el usuario, incluye
   capturas de pantalla.
7. Ten en cuenta que todo el texto visible para el usuario final debe estar
   en español (el proyecto no tiene selector de idioma).

¿Encontraste un error o tenés una idea? Abre un *issue* en GitHub describiendo
el problema o la propuesta antes de invertir tiempo en una implementación
grande, para alinear el enfoque con el resto del proyecto.

# English version

## Azure deployment

The production AppHost model targets Azure Container Apps in Brazil South. It
publishes the Web frontend and internal API as Container Apps, the migration
worker as a manually triggered Container App Job, PostgreSQL as Azure Database
for PostgreSQL Flexible Server, and Azurite as an Azure Storage account.

The repository is configured with these low-cost launch defaults:

- Resource group: `rg-terremoto-prod`
- Region: `brazilsouth`
- PostgreSQL: version 16, `Standard_B1ms`, 32 GB, 7-day backups, no HA
- Storage: Standard LRS with anonymous reads allowed for individual report blobs
- Web replicas: exactly 1; API replicas: 0-3
- Public ingress: Web only
- Existing Azure Container Apps environment: `quakereportenvyksjkeaewt`

### Prerequisites

Install and authenticate the Aspire CLI, Azure CLI, and Docker. Select the
intended Azure subscription before publishing:

```powershell
az login
az account set --subscription "<subscription-id-or-name>"
docker version
aspire --version
```

If a terminal opened before the CLIs were installed, restart it or add these
installation folders to that terminal's `PATH`:

```powershell
$env:Path += ";$env:USERPROFILE\.aspire\bin;C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin"
```

### Configuration and secrets

Azure location and resource-group defaults are committed in the AppHost's
production settings. Set the deployment values explicitly in Aspire's local
configuration, and keep both Google keys in the AppHost user-secret store. The
public key is used by the browser map and should be restricted by HTTP referrer.
The private key is used only by server-side Places and Geocoding requests:

```powershell
aspire config set "Azure:SubscriptionId" "<subscription-id>"
aspire config set "Azure:Location" "brazilsouth"
aspire config set "Azure:ResourceGroup" "rg-terremoto-prod"
aspire secret set "Parameters:existing-aca-environment-name" "quakereportenvyksjkeaewt" `
  --apphost QuakeReport.AppHost/QuakeReport.AppHost.csproj
aspire secret set "Parameters:existing-aca-environment-resource-group" "rg-terremoto-prod" `
  --apphost QuakeReport.AppHost/QuakeReport.AppHost.csproj
aspire secret set "Parameters:google-maps-api-key" "<google-api-key>" `
  --apphost QuakeReport.AppHost/QuakeReport.AppHost.csproj
aspire secret set "Parameters:google-maps-private-api-key" "<google-private-api-key>" `
  --apphost QuakeReport.AppHost/QuakeReport.AppHost.csproj
```

For CI, provide the same values as `Azure__SubscriptionId`,
`Parameters__google_maps_api_key`, and
`Parameters__google_maps_private_api_key` environment variables.

### Validate without provisioning

```powershell
dotnet build QuakeReport.slnx
dotnet test --no-build --filter TestCategory=Unit
aspire deploy --apphost QuakeReport.AppHost/QuakeReport.AppHost.csproj `
  --environment Production --list-steps
aspire publish --apphost QuakeReport.AppHost/QuakeReport.AppHost.csproj `
  --environment Production --output-path ./aspire-output
```

Review `aspire-output` before deploying. Secret values must remain parameterized.
The production output must contain Azure Container Apps, ACR, PostgreSQL,
Storage, managed identities, and a migration job; it must not contain PostgreSQL,
Azurite, or Redis containers.

### Deploy and migrate

```powershell
aspire deploy --apphost QuakeReport.AppHost/QuakeReport.AppHost.csproj `
  --environment Production
```

The migration resource is a manual one-time job. After provisioning, list the
generated names, start the migration job, and wait for a successful execution:

```powershell
$resourceGroup = "rg-terremoto-prod"
az containerapp job list --resource-group $resourceGroup --output table
az containerapp job start --resource-group $resourceGroup --name "<migration-job-name>"
az containerapp job execution list --resource-group $resourceGroup `
  --name "<migration-job-name>" --output table
```

Do not promote the deployment until the latest migration execution reports
`Succeeded`. Then obtain the frontend URL and verify its health endpoint:

```powershell
az containerapp list --resource-group $resourceGroup --output table
Invoke-WebRequest "https://<web-frontend-fqdn>/health"
```

### Connect terremoto.com.co through Cloudflare

The AppHost declaratively registers `terremoto.com.co` and
`www.terremoto.com.co` on the frontend and binds the uploaded
`terremoto-cloudflare` Origin CA certificate. Do not add these hostnames only
through the Azure portal: an Aspire deployment reconciles the Container App to
the AppHost model and removes out-of-band hostname changes.

Get the Container Apps environment static IP, frontend FQDN, and domain
verification value from Azure. In Cloudflare DNS add:

- Proxied `A @` to the Container Apps environment static IP.
- `TXT asuid` with the frontend's domain-verification value.
- Proxied `CNAME www` directly to the generated frontend FQDN.
- `TXT asuid.www` with the same verification value.

Set Cloudflare SSL/TLS encryption mode to **Full (strict)**. The application
permanently redirects `www` to the apex domain while preserving paths and query
strings. If the Container Apps environment or uploaded certificate is replaced,
update the `cloudflare-certificate-id` default in the AppHost before deploying.

### Rollback

Database migrations in this project are forward-only; do not roll back the
database automatically. To roll back application code, list revisions and move
traffic to the last healthy Web and API revisions:

```powershell
az containerapp revision list --resource-group $resourceGroup `
  --name "<container-app-name>" --output table
az containerapp ingress traffic set --resource-group $resourceGroup `
  --name "<container-app-name>" --revision-weight "<healthy-revision>=100"
```

Fix the application or add a corrective EF migration, then run `aspire deploy`
again. Do not use `aspire destroy` as a rollback mechanism because it deletes
the deployment resource group.

### Missing-person registry configuration

The Phase 2 registry requires three deployment parameters. Set them as Aspire
secrets before deployment; never commit their values:

```powershell
aspire param set missing-person-id-hmac-key "<long-random-secret>" --environment Production
aspire param set turnstile-secret-key "<cloudflare-turnstile-secret>" --environment Production
aspire param set turnstile-site-key "<cloudflare-turnstile-site-key>" --environment Production
aspire param set moderation-api-key "<long-random-internal-key>" --environment Production
aspire param set cloudflare-access-team-domain "<team-name>.cloudflareaccess.com" --environment Production
aspire param set cloudflare-access-audience "<cloudflare-access-audience>" --environment Production
```

Configure the Turnstile site for `terremoto.com.co` and
`www.terremoto.com.co`. The API stores only HMAC document identifiers and
hashed recovery codes. Run the migration job after deployment before using
the registry. The recovery code is shown once after publication and must not
be placed in a URL.

### Collection-point registry and moderation

The collection-point registry uses the active earthquake and the same migration
job as the rest of the application. Community submissions are visible as
**No verificado** until approved; official entries are created as **Oficial**.
The management code is returned only once and is never stored in plain text.

Protect `/acopios/admin*` and `/refugios/admin*` in Cloudflare Access for both
`terremoto.com.co` and `www.terremoto.com.co`. Configure approved moderator
email addresses and one-time PIN authentication. Production validates the
Cloudflare Access JWT issuer, audience, signature, and expiry, and fails closed
when the team domain or audience is missing. The Web app forwards only the
internal moderation credential to the internal API.

After deploying the migration, start the migration Container App Job and verify
it succeeds before publishing the collection-point URL. Public users can
submit a point, comments, and abuse reports with Turnstile; moderators review
pending entries at `/acopios/admin`.

### Shelter registry

Community shelters are published immediately as **No verificado** and can be
managed with the one-time recovery code returned at creation. Moderators can
approve, reject, fully edit, change the operational status, or create official
shelters at `/refugios/admin`. Address lookup is optional: typed addresses are
stored without coordinates and generate an address-based Google Maps link.

After deployment, run the migration Container App Job so the `Shelters` and
`ShelterAbuseReports` tables from `AddShelters` are created before smoke testing
the public and moderator flows.

### Automated social-media ingestion

The API exposes a separate, key-protected ingestion surface for structured data
extracted by an external AI process. It does not accept Turnstile tokens and it
cannot approve or create official records. Imported collection points, blood
donation centers, shelters, and help requests are created as **No verificado**
with `Automated` source status and remain subject to normal moderation.

Configure the key as an Aspire secret:

```powershell
aspire param set ingestion-api-key "<random-32-byte-value>" --environment Production
```

Use the public Web relay and send both headers. Never place the key in a URL or
commit it:

```http
POST https://terremoto.com.co/api/ingestion/v1/blood-donation-centers
X-Ingestion-Api-Key: <ingestion-api-key>
Idempotency-Key: <unique-key-for-the-source-post>
Content-Type: application/json
```

Available paths are `/collection-points`, `/blood-donation-centers`,
`/shelters`, and `/help-requests`. Every request must include a public HTTPS
source URL, extraction confidence, and structured entity data. Repeating an
`Idempotency-Key` returns the original submission instead of creating a second
record. The API resolves the active earthquake server-side and rejects imports
when no active earthquake exists.

After deployment, run the migration Container App Job before sending imports.
Review automated entries through the existing moderator pages and verify the
original source before approving them.
