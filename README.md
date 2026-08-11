# QuakeReport

## Descripción

QuakeReport es una herramienta para que la ciudadanía reporte daños causados
por un terremoto: describe lo ocurrido, indica la gravedad y el tipo de daño,
comparte su ubicación (por GPS o buscando la dirección) y adjunta fotos o
videos como evidencia. Los reportes se listan de mayor a menor impacto, para
ayudar a priorizar la respuesta donde más se necesita. Este MVP nace a raíz
del terremoto en Colombia y está disponible en terremoto.com.co.

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

### Configuration and secret

Azure location and resource-group defaults are committed in the AppHost's
production settings. Set the deployment values explicitly in Aspire's local
configuration, and keep the Google key in the AppHost user-secret store:

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
```

For CI, provide the same values as `Azure__SubscriptionId` and
`Parameters__google_maps_api_key` environment variables.

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
