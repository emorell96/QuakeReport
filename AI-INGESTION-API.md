# AI Ingestion API

This API allows an external AI process to submit structured information extracted from public social-media posts. It supports collection points, blood donation centers and drives, shelters, and rescue help requests.

Imported records are created as **Automated** and **Pending**. They appear as **No verificado** until a moderator reviews them. The API cannot approve records or create official entries.

## Base URL

Production:

~~~text
https://terremoto.com.co/api/ingestion/v1
~~~

Available endpoints:

~~~text
POST /collection-points
POST /blood-donation-centers
POST /shelters
POST /help-requests
~~~

## Authentication

Every request requires:

~~~http
X-Ingestion-Api-Key: <ingestion-api-key>
Idempotency-Key: <unique-value-for-this-source-post>
Content-Type: application/json
~~~

The key is configured as the Aspire secret \`ingestion-api-key\`. Never place it in a URL, JSON body, social-media post, or source-control repository.

\`Idempotency-Key\` prevents duplicate records when a request is retried. Reusing the same key with the same entity type returns the original submission.

## Common source object

Every request contains a \`source\` object and a typed \`data\` object:

~~~json
{
  "source": {
    "platform": 0,
    "sourceUrl": "https://x.com/example/status/123",
    "externalPostId": "123",
    "publishedAt": "2026-08-12T10:00:00Z",
    "extractedAt": "2026-08-12T10:05:00Z",
    "confidence": 0.92,
    "evidenceSummary": "El centro informa que recibe agua y alimentos."
  },
  "data": {}
}
~~~

\`sourceUrl\` must be a public HTTPS URL. \`confidence\` must be between 0 and 1. Platform values are:

| Value | Platform |
|---:|---|
| 0 | X |
| 1 | Facebook |
| 2 | Instagram |
| 3 | WhatsApp |
| 4 | Website |
| 5 | Other |

Dates must be ISO 8601 timestamps. The active earthquake is assigned by the server; clients must not send an earthquake ID.

## Collection points

~~~text
POST /collection-points
~~~

~~~json
{
  "source": {
    "platform": 0,
    "sourceUrl": "https://x.com/example/status/123",
    "externalPostId": "123",
    "confidence": 0.9
  },
  "data": {
    "name": "Centro comunitario San José",
    "organizationName": "Fundación San José",
    "address": "Calle 10 # 20-30, Cali, Colombia",
    "latitude": 3.4516,
    "longitude": -76.532,
    "description": "Punto de recepción de donaciones.",
    "needsSummary": "Agua, alimentos no perecederos y cobijas",
    "receivingInstructions": "Recibe donaciones de 8:00 a.m. a 5:00 p.m.",
    "contactName": "Coordinación",
    "contactPhone": "+57 300 000 0000",
    "contactWhatsApp": null,
    "contactEmail": null,
    "endsAt": null
  }
}
~~~

\`address\`, \`needsSummary\`, and \`receivingInstructions\` are required. Coordinates are optional; a manual address is valid without Google Maps coordinates.

## Blood donation centers and drives

~~~text
POST /blood-donation-centers
~~~

~~~json
{
  "source": {
    "platform": 1,
    "sourceUrl": "https://www.facebook.com/example/posts/123",
    "externalPostId": "123",
    "confidence": 0.95
  },
  "data": {
    "name": "Campaña de donación de sangre",
    "organizationName": "Hospital Central",
    "address": "Carrera 5 # 10-20, Cali, Colombia",
    "latitude": null,
    "longitude": null,
    "description": "Campaña temporal para apoyar al hospital.",
    "operatingInstructions": "Confirmar requisitos directamente con el hospital.",
    "needsSummary": "Se requieren donantes de sangre tipo O positivo.",
    "publicPhone": "+57 300 000 0000",
    "publicWhatsApp": null,
    "publicEmail": null,
    "centerType": 1,
    "bloodTypes": 64,
    "components": 1,
    "startsAt": "2026-08-12T08:00:00Z",
    "endsAt": "2026-08-12T17:00:00Z"
  }
}
~~~

\`centerType\`: \`0\` permanent site, \`1\` temporary campaign.

Blood-type flags:

- \`1\` A+
- \`2\` A-
- \`4\` B+
- \`8\` B-
- \`16\` AB+
- \`32\` AB-
- \`64\` O+
- \`128\` O-
- \`256\` Unknown

Component flags:

- \`1\` whole blood
- \`2\` red blood cells
- \`4\` plasma
- \`8\` platelets
- \`16\` unknown

Combine multiple flags by adding their values. At least one blood type and one component are required. Temporary campaigns require valid start and end dates.

## Shelters

~~~text
POST /shelters
~~~

~~~json
{
  "source": {
    "platform": 4,
    "sourceUrl": "https://example.org/emergency-update",
    "confidence": 0.88
  },
  "data": {
    "name": "Coliseo Municipal",
    "organizationName": "Alcaldía Municipal",
    "address": "Avenida Central, Cali, Colombia",
    "latitude": 3.45,
    "longitude": -76.53,
    "description": "Refugio temporal para familias afectadas.",
    "operatingInstructions": "Registro disponible las 24 horas.",
    "contactName": "Coordinación del refugio",
    "contactPhone": "+57 300 000 0000",
    "contactWhatsApp": null,
    "contactEmail": null
  }
}
~~~

\`name\`, \`address\`, \`description\`, and \`operatingInstructions\` are required.

## Rescue help requests

~~~text
POST /help-requests
~~~

~~~json
{
  "source": {
    "platform": 0,
    "sourceUrl": "https://x.com/rescue-team/status/456",
    "externalPostId": "456",
    "confidence": 0.91
  },
  "data": {
    "title": "Se necesita maquinaria para rescate",
    "requesterName": "Grupo de rescate Cali",
    "organizationName": "Rescate Voluntario",
    "address": "Barrio El Centro, Cali, Colombia",
    "latitude": null,
    "longitude": null,
    "needDetails": "Se necesita maquinaria pesada y personal especializado.",
    "instructions": "Coordinar primero por teléfono.",
    "publicPhone": "+57 300 000 0000",
    "publicWhatsApp": null,
    "publicEmail": null,
    "priority": 3,
    "needCategories": 8,
    "neededBy": "2026-08-12T18:00:00Z"
  }
}
~~~

Priority values: \`0\` Low, \`1\` Medium, \`2\` High, \`3\` Critical.

Need-category flags:

- \`1\` personnel
- \`2\` medical assistance or supplies
- \`4\` rescue equipment
- \`8\` machinery
- \`16\` transportation
- \`32\` food and water
- \`64\` communications
- \`128\` temporary shelter
- \`256\` security
- \`512\` other

Combine multiple categories by adding their values. A public phone or WhatsApp number is required.

## Response

Successful creation returns \`201 Created\`:

~~~json
{
  "submissionId": "guid",
  "entityId": "guid",
  "entityType": 1,
  "moderationStatus": "Pending",
  "duplicate": false,
  "publicPath": "/donacion-sangre/guid"
}
~~~

A replayed idempotency key returns \`200 OK\` with \`duplicate: true\`.

## Error responses

| Status | Meaning |
|---:|---|
| 400 | Invalid JSON, missing headers, invalid source, or validation error |
| 401 | Missing or invalid API key |
| 409 | Conflicting source post or idempotency key |
| 413 | Request body too large |
| 422 | No active earthquake is configured |
| 429 | Ingestion rate limit exceeded |

The API does not automatically verify social-media claims. The AI should submit only facts supported by the source, include the source URL, and avoid copying private information or the complete original post.

