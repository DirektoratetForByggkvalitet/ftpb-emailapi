# EmailService (FT-1999) — status for drift

`ftpb-email-api` går fra .NET 8 til .NET 10, kjøres som container, og kvitter seg med client secret
mot Microsoft Graph. Koden er klar. **Ingenting kan deployes før det finnes et container registry** —
det er den eneste harde blokkereren.

Detaljene ligger i `FT-1999-drift-discussion.md`; dette er kortversjonen.

## Klart fra vår side

- Appen bygges som container. Bicep peker web app-en mot ACR og henter imaget med appens egen
  managed identity — ingen registry-passord noe sted.
- **Ingen hemmeligheter mot Graph.** Appen kjører som en user-assigned managed identity
  (`uid-ftpb-email-api`), som registreres som *federated identity credential* på den app-registreringen
  vi allerede bruker. Managed identity-tokenet veksles inn i et app-token. Ingenting utløper, og
  app-registreringen beholder Graph-rettighetene og postkasse-avgrensningen den har i dag.
  Sertifikatløsningen som lå i grenen tidligere er forkastet — dette er forslaget fra drift, og det er
  bedre.
- **Staging-sloten er fjernet.** Deploy blir: sett image → restart.
- Bicep setter seks app settings; resten leser appen fra Azure App Configuration, slik core-functions
  gjør.
- `/health` er nå uten avhengigheter — det er den App Service prober. `/health/ready` sjekker Graph.
- Pipeline bygger imaget med `az acr build` og lager Octopus-release. Image-tag = release-versjon,
  aldri `latest`.

## Må gjøres av drift — rekkefølgen betyr noe

| # | Oppgave | Hvem |
| --- | --- | --- |
| 1 | **ACR per miljø**, hver i egen ressursgruppe. Octopus-tjenestepersonen må ha RBAC-skriv der. **Blokkerer alt annet.** | Drift |
| 2 | Autentisering for pipeline mot ACR. Helst Bitbucket OIDC + federated credential med `AcrPush`; vi vil unngå registry admin. | Drift |
| 3 | Binde bicep-parametere og **kjøre runbooken i dev**. Den oppretter `uid-ftpb-email-api` og gir ut identitetens `principalId` og `clientId` som output — de trengs i punkt 4. Runbooken kjørte sist i 2023. | Drift |
| 4 | **Federated identity credential på app-registreringen** bak `GraphApiAuth:ClientId`: subject = identitetens `principalId` fra punkt 3, issuer = `https://login.microsoftonline.com/<tenantId>/v2.0`, audience = `api://AzureADTokenExchange`. Krever Application Administrator eller eierskap på app-registreringen. | Wulff / DiBK |
| 5 | `App Configuration Data Reader` for `uid-ftpb-email-api`, per miljø. Appen leser hele store-et, slik core-functions gjør — verdier med miljø-label overstyrer dem uten label, og nøkler under `EMAIL/` er appens egne. | Drift |
| 6 | Lesetilgang i Key Vault for identiteten — **kun** for de hemmelighetene App Configuration slår opp via Key Vault-referanse (Elastic-passord, APM-token). Helst scoped til objektene, ikke hele hvelvet; dette er delte hvelv. Utgår hvis de verdiene ligger rett i App Configuration. | Drift |
| 7 | Octopus-variabler: `AppConfiguration_Uri` og `Azure:TenantId` legges til. `GraphApiAuth:TenantId` og `GraphApiAuth:ClientId` beholdes. `GraphApiAuth:ClientSecret` pensjoneres — og credentialen slettes i Entra, den blir ikke tilbakekalt av å fjerne variabelen. | Drift |
| 8 | Deployment-prosess: erstatt `deploy app` med `az webapp config container set` + restart, fjern start/swap/stop slot-stegene, og slett `staging`-sloten i Azure. | Drift |

Merk rekkefølgen på 3 og 4: identiteten må finnes før credentialen kan peke på den, og en feil
subject gir ingen feilmelding ved oppretting — den dukker først opp når token-vekslingen feiler.

## To spørsmål vi trenger svar på

1. Er det én app-registrering for alle tre miljøene, eller én per miljø? Hvert miljø har sin egen
   identitet, så det trengs én federated credential per identitet.
2. Hvilke **label**-verdier brukes per miljø i App Configuration? `ASPNETCORE_ENVIRONMENT` må matche
   dem, ellers leser dev produksjonsverdier.

## Hva dette løser

FT-1999 handler om å redusere risiko knyttet til utløp av hemmeligheter. Med denne løsningen finnes
det ingen hemmelighet igjen å rotere: ingen client secret, ingen sertifikater, ingen utløpsdato å
holde styr på. Behovet for et eget rotasjonsregime — og oppfølgingspunktet under `AIIDVOI-206` —
faller dermed bort.

## Når er vi i mål i dev

Punkt 1–8 på plass → første release → `/health` og `/health/ready` svarer Healthy → testmail kommer
frem → loggen dukker opp i Elastic.
