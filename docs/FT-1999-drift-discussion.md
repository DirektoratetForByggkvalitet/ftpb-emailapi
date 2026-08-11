# FT-1999 `ftpb-email-api` — what we need from drift

*Updated 2026-08-11, after the decisions below were taken. The detailed branch review is in
`FT-1999-email-api-container-migration.md`.*

`ftpb-email-api` moves to .NET 10, a container image, and certificate-based authentication against
Microsoft Graph. The application side is being finished on `feature/FT-1999`. This document is the
drift side: what has been decided, and what we need drift to do.

**The blocking prerequisite is that no ACR exists in the DiBK subscription.** Until one does, there
is nowhere to push the image and nothing can be deployed. Everything else follows from that.

## Decisions taken on our side

These are settled — they are listed so drift knows what the asks below assume, not to reopen them.

| # | Decision | Consequence for drift |
| --- | --- | --- |
| 1 | **The staging slot is dropped.** The app is deployed like `ftpb-pdfgenerator`: set the image, restart. | The `start slot` / `swap` / `stop slot` steps come out of the deployment process, and the existing `staging` slot should be deleted in Azure. An incremental ARM deploy will not remove it by itself. |
| 2 | **One identity, system-assigned.** With no slot there is only one identity to grant, so a user-assigned identity is not worth introducing. | Each grant (ACR, Key Vault, App Configuration) is made once, against the web app's system-assigned identity. |
| 3 | **The deployment process owns the container image tag; Bicep does not set it.** The tag equals the Octopus release version and is never `latest`. | A Bicep runbook run cannot re-point the app at an old image. |
| 4 | **Configuration is read from Azure App Configuration** (`app-configuration-<env>-dibk.azconfig.io`), as `ftpb-core-functions` does. | The Bicep template declares only four app settings, so the runbook is not a config bottleneck. Shared values (Elastic, APM) live in one place instead of being duplicated per project. |
| 5 | **Health is split.** `/health` is a dependency-free liveness endpoint and is what App Service probes; `/health/ready` reports Graph connectivity. | App Service no longer recycles instances because Key Vault or Entra had a blip. |

Under decision 4 the template sets only `WEBSITES_PORT`, `ASPNETCORE_ENVIRONMENT`,
`AppConfiguration_Uri` and `Azure__TenantId`. Everything else — including secrets, via the store's
own Key Vault references — comes from App Configuration.

## What we need from drift, in order

Out of order this fails confusingly, so the order matters more than the individual items.

### 1. A container registry per environment — blocks everything else

One ACR per environment, each in its own resource group. The Octopus service principal needs
RBAC-write in those resource groups, because the template assigns `AcrPull` cross-resource-group.

Accepted consequence: the image is rebuilt per environment rather than promoted, so dev/test/prod do
not run a bit-identical artifact.

### 2. Pipeline authentication to the registry

We would rather not put a long-lived registry credential in Bitbucket. In preference order:

1. **Bitbucket OIDC with a federated credential** on an Entra service principal holding `AcrPush`
   (this is what the pipeline is currently written against).
2. An Entra service principal with `AcrPush` and a client secret.
3. An ACR scope-map token.

We would rather not enable the registry admin account. The build runs as an ACR Task
(`az acr build`), so no Docker daemon or registry password is needed in the pipeline.

### 3. The Graph certificate

1. Certificate into `ft-kv-dev`, `ftpb-kv-test`, `ft-kv-prod` (drift).
2. Its **public key** onto the app registration behind `GraphApiAuth:ClientId` (Wulff — DiBK owns
   the app registrations).
3. The old `ClientSecret` credential deleted in Entra after cutover. Removing the Octopus variable
   does **not** revoke it.

### 4. Key Vault access, scoped as narrowly as possible

The app downloads the certificate at runtime, which reads both the certificate **and its backing
secret** (that is where the private key lives) — a certificate-read grant alone is not enough.
`Key Vault Secrets User` is required.

**These are the shared vaults**, so a vault-scoped grant would give this app read access to every
other project's secrets. We ask for access **scoped to the individual objects**: the Graph
certificate, plus any secrets App Configuration resolves by reference for us. If object-scoped
assignment is not workable, we would like to know before we build it, and would then discuss a
dedicated vault for this app.

**We need to know:** does the Octopus service principal hold `User Access Administrator` or
`RBAC Administrator` on the vaults? If yes, the grant can live in Bicep and re-applies itself. If
no, it is a manual grant and we will write that into the procedure instead.

### 5. Azure App Configuration

1. Confirm the store is the right home for this app's configuration.
2. Agree the key naming scheme. The app currently reads two prefixes and trims both: `CF/` for
   values shared across FTPB applications, and `EMAIL/` for its own. What matters is that the
   genuinely shared values — the Elastic endpoint and credentials, the APM server and token — sit
   under one prefix that both this app and core-functions read, rather than two copies drifting
   apart. If drift prefers a different name than `CF/` for the shared prefix, say so and we will
   change it; it is one constant.
3. `App Configuration Data Reader` for the app identity, per environment.
4. Confirm the **label** values used per environment, since `ASPNETCORE_ENVIRONMENT` must match them.

Note: configuration is read at startup only, as in core-functions. A configuration change therefore
needs an app restart — roughly what app settings do today anyway.

### 6. Octopus variables

Add as **environment-scoped** variables: `GraphApiAuth:KeyVaultUri`,
`GraphApiAuth:CertificateName`, `AppConfiguration_Uri`, `Azure:TenantId`.

Retire `GraphApiAuth:ClientSecret` (project-scoped only, so no blast radius) — and delete the
credential in Entra afterwards, per item 3.

Values that move into App Configuration instead of Octopus: the Serilog/Elastic settings, the
ElasticApm settings, and `GraphApiEmailSettings`.

### 7. The Bicep runbook

Bind the template's parameters — `acrName`, `acrResourceGroup`, `aspNetCoreEnvironment`,
`appConfigurationUri`, `azureTenantId` — and **run the runbook in dev before the first container
release**. It last ran in 2023, so it is worth treating the first run as a test in its own right.

### 8. The deployment process

Replace the current process with the `ftpb-pdfgenerator` shape:

1. `az webapp config container set --docker-custom-image-name <acr>.azurecr.io/<image>:<tag>`
   (tag = the release version)
2. restart the web app

Remove the `start slot`, `swap` and `stop slot` steps, and delete the `staging` slot in Azure.

### 9. Certificate expiry and rotation — needs an owner

Rotation is a two-sided operation: renewing the certificate in Key Vault is not enough on its own,
the new public key must also be uploaded to the app registration, and the app picks up a new
certificate only on restart (the credential is held for the process lifetime). We would like the
expiry tracked — proposed under `AIIDVOI-206` — and the procedure written down rather than
rediscovered at expiry.

## Open question we are testing ourselves

Does ARM preserve `linuxFxVersion` when the template omits it? If it resets the value instead, a
runbook run would leave the app without an image until the next release, and we will put a required
`imageTag` parameter back. It is a ten-minute test in dev and it is ours to run, not drift's.


## Definition of ready — updated 2026-08-11

Branch:

- [x] B1 — configuration read from Azure App Configuration; the template declares
      `WEBSITES_PORT`, `ASPNETCORE_ENVIRONMENT`, `AppConfiguration_Uri`, `Azure__TenantId`
- [x] ~~B2~~ — void: the staging slot is gone, so nothing is shared with it
- [ ] B3 — Key Vault access for the site identity (in Bicep, or confirmed manual with a written
      procedure). Blocked on whether the Octopus SP has RBAC-write on the vaults
- [ ] B4 — `deploy app` replaced with `az webapp config container set` + restart (drift, no slot involved)
- [x] ~~S1~~ — void: no swap, so no warm-up ping needed
- [x] S2 — `/health` is liveness-only; the Graph check moved to `/health/ready`
- [x] S3 — `az acr build` replaces `docker build`/`docker push`
- [x] S4 — release creation + `develop` / `release/rc-*` / `master` sections in the pipeline
      (house naming, so no branch rename is needed after all)
- [x] S6 — `startCommand` removed
- [x] `check-package` run and confirmed: `Azure.Security.KeyVault.Certificates` 4.9.0 (MIT) and
      `Microsoft.Extensions.Configuration.AzureAppConfiguration` 8.6.0 (MIT, verified from the
      upstream LICENSE — deps.dev reports it as `non-standard`)

Outside — see `FT-1999-drift-discussion.md` for the full ask:

- [ ] ACR created per environment, Octopus SP has RBAC-write in its RG
- [ ] Pipeline authentication to ACR (Bitbucket OIDC federated credential preferred)
- [ ] Certificate in all three vaults and on the app registration
- [ ] Key Vault and App Configuration grants for the site identity
- [ ] Octopus variables added and `ClientSecret` retired
- [ ] Runbook params bound and the runbook run in dev
- [ ] Deployment process replaced, `staging` slot deleted
