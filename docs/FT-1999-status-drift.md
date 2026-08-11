# EmailService (FT-1999) — status for drift

`ftpb-email-api` går fra .NET 8 til .NET 10, kjøres som container, og bytter fra client secret til
sertifikat mot Microsoft Graph. Koden er klar. **Ingenting kan deployes før det finnes et container
registry** — det er den eneste harde blokkereren.

Detaljene ligger i `FT-1999-drift-discussion.md`; dette er kortversjonen.

## Klart fra vår side

- Appen bygges som container. Bicep peker web app-en mot ACR og henter imaget med web app-ens egen
  managed identity — ingen registry-passord noe sted.
- **Staging-sloten er fjernet.** Deploy blir: sett image → restart.
- Bicep setter bare fire app settings: `WEBSITES_PORT`, `ASPNETCORE_ENVIRONMENT`,
  `AppConfiguration_Uri`, `Azure__TenantId`. Resten leser appen fra Azure App Configuration, slik
  core-functions gjør.
- `/health` er nå uten avhengigheter — det er den App Service prober. `/health/ready` sjekker Graph.
- Pipeline bygger imaget med `az acr build` og lager Octopus-release. Image-tag = release-versjon,
  aldri `latest`.

## Må gjøres av drift — rekkefølgen betyr noe

| # | Oppgave | Hvem |
| --- | --- | --- |
| 1 | **ACR per miljø**, hver i egen ressursgruppe. Octopus-tjenestepersonen må ha RBAC-skriv der. **Blokkerer alt annet.** | Drift |
| 2 | Autentisering for pipeline mot ACR. Helst Bitbucket OIDC + federated credential med `AcrPush`; vi vil unngå registry admin. | Drift |
| 3 | Sertifikat inn i `ft-kv-dev`, `ftpb-kv-test`, `ft-kv-prod` — og offentlig nøkkel opp på app-registreringen bak `GraphApiAuth:ClientId`. | Drift / Wulff |
| 4 | Lesetilgang til sertifikatet for web app-ens identitet. `Key Vault Secrets User` trengs, fordi privatnøkkelen ligger i sertifikatets secret. Helst scoped til objektet, ikke hele hvelvet — dette er delte hvelv. | Drift |
| 5 | `App Configuration Data Reader` for identiteten per miljø, og enighet om nøkkelprefiks (`CF/` delt, `EMAIL/` for denne appen). | Drift |
| 6 | Octopus-variabler: `GraphApiAuth:KeyVaultUri`, `GraphApiAuth:CertificateName`, `AppConfiguration_Uri`, `Azure:TenantId`. `ClientSecret` pensjoneres — og credentialen slettes i Entra, den blir ikke tilbakekalt av å fjerne variabelen. | Drift |
| 7 | Binde bicep-parametere (`acrName`, `acrResourceGroup`, `aspNetCoreEnvironment`, `appConfigurationUri`, `azureTenantId`) og kjøre runbooken i dev **før** første release. Den kjørte sist i 2023. | Drift |
| 8 | Deployment-prosess: erstatt `deploy app` med `az webapp config container set` + restart, fjern start/swap/stop slot-stegene, og slett `staging`-sloten i Azure. | Drift |
| 9 | Eier for sertifikatutløp og rotasjon (foreslått under `AIIDVOI-206`). Rotasjon er tosidig: nytt sertifikat i Key Vault **og** ny offentlig nøkkel på app-registreringen, og appen plukker det opp først ved restart. | Drift / Wulff |

## To spørsmål vi trenger svar på

1. Har Octopus-tjenestepersonen `User Access Administrator` eller `RBAC Administrator` på hvelvene?
   Avgjør om tilgangen i punkt 4 kan ligge i Bicep, eller må settes manuelt.
2. Hvilke **label**-verdier brukes per miljø i App Configuration? `ASPNETCORE_ENVIRONMENT` må matche
   dem, ellers leser dev produksjonsverdier.

## Når er vi i mål i dev

Punkt 1–8 på plass → runbook kjører rent → første release → `/health` og `/health/ready` svarer
Healthy → testmail kommer frem → loggen dukker opp i Elastic.
