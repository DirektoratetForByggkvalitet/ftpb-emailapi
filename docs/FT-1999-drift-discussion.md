# FT-1999 `ftpb-email-api` — what we need from drift

*Updated 2026-08-11, after the decisions below were taken. The detailed branch review is in
`FT-1999-email-api-container-migration.md`.*

`ftpb-email-api` moves to .NET 10, a container image, and secret-free authentication against
Microsoft Graph. The application side is being finished on `feature/FT-1999`. This document is the
drift side: what has been decided, and what we need drift to do.

*Revised after drift's feedback that there is no certificate setup for Entra today, and that managed
identity is what requires least maintenance. That is right, and the branch now uses a federated
identity credential instead of a certificate — see decision 2b.*

**The blocking prerequisite is that no ACR exists in the DiBK subscription.** Until one does, there
is nowhere to push the image and nothing can be deployed. Everything else follows from that.

## Decisions taken on our side

These are settled — they are listed so drift knows what the asks below assume, not to reopen them.

| # | Decision | Consequence for drift |
| --- | --- | --- |
| 1 | **The staging slot is dropped.** The app is deployed like `ftpb-pdfgenerator`: set the image, restart. | The `start slot` / `swap` / `stop slot` steps come out of the deployment process, and the existing `staging` slot should be deleted in Azure. An incremental ARM deploy will not remove it by itself. |
| 2 | **A user-assigned identity, `uid-ftpb-email-api`.** Required, not merely preferred: a federated identity credential cannot be configured on a system-assigned identity. | Each grant (ACR, App Configuration, Key Vault) is made once, and survives the web app being recreated. |
| 2b | **No credential against Graph at all.** The identity is registered as a *federated identity credential* on the existing app registration, and its token is exchanged for an app token. Replaces the certificate approach, on drift's recommendation. | No certificate to install, no rotation regime, and the app registration keeps its current Graph permissions and mailbox scoping. |
| 3 | **The deployment process owns the container image tag; Bicep does not set it.** The tag equals the Octopus release version and is never `latest`. | A Bicep runbook run cannot re-point the app at an old image. |
| 4 | **Configuration is read from Azure App Configuration** (`app-configuration-<env>-dibk.azconfig.io`), as `ftpb-core-functions` does. | The Bicep template declares only four app settings, so the runbook is not a config bottleneck. Shared values (Elastic, APM) live in one place instead of being duplicated per project. |
| 5 | **Health is split.** `/health` is a dependency-free liveness endpoint and is what App Service probes; `/health/ready` reports Graph connectivity. | App Service no longer recycles instances because Key Vault or Entra had a blip. |

Under decision 4 the template sets six app settings: `WEBSITES_PORT`, `ASPNETCORE_ENVIRONMENT`,
`AppConfiguration_Uri`, `Azure__TenantId`, and — derived from the identity, so nothing to bind —
`AZURE_CLIENT_ID` and `GraphApiAuth__ManagedIdentityClientId`. Everything else, including secrets via
the store's own Key Vault references, comes from App Configuration.

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

### 3. Federated identity credential on the app registration

This replaces everything the certificate approach needed. **It has to happen after the runbook has
run** (item 7), because the credential's subject is the identity's principal id and the identity is
created by the template.

On the app registration behind `GraphApiAuth:ClientId`, add a federated credential:

| Field | Value |
| --- | --- |
| Scenario | Managed identity |
| subject | the `managedIdentityPrincipalId` output from the runbook — must match exactly |
| issuer | `https://login.microsoftonline.com/<tenantId>/v2.0` |
| audience | `api://AzureADTokenExchange` |

Requires Application Administrator, Cloud Application Administrator, or ownership of the app
registration — so this is Wulff / DiBK.

Two things worth knowing: a wrong subject, issuer or audience is accepted without error and only
surfaces when the token exchange fails at runtime; and propagation takes a little time after the
credential is created.

Afterwards, delete the old `ClientSecret` credential in Entra. Removing the Octopus variable does
**not** revoke it.

Note that the app registration keeps its existing Graph permissions and whatever mailbox scoping it
has today — we are changing how the app proves it is that app registration, nothing else.

### 4. Key Vault access — only if App Configuration uses Key Vault references

With the certificate gone, the app no longer reads Key Vault directly for authentication. The only
remaining need is for secrets the App Configuration store resolves by reference on our behalf — the
Elastic password and the APM token. If those live directly in App Configuration, this item drops
entirely.

If they are Key Vault references: `Key Vault Secrets User` for `uid-ftpb-email-api`, **scoped to the
individual secrets**. These are the shared vaults, so a vault-scoped grant would give this app read
access to every other project's secrets.

### 5. Azure App Configuration

1. Confirm the store is the right home for this app's configuration.
2. Agree the key naming scheme. The app loads the **whole store** — unlabelled values first, then
   values labelled with the environment, which override them — exactly as core-functions does. Keys
   under `EMAIL/` are trimmed, so this app's own values can be named without colliding with anything
   else, and they override a shared value of the same name. What matters is that the genuinely
   shared values — the Elastic endpoint and credentials, the APM server and token — are defined once
   and read by both applications, rather than as two copies drifting apart.
3. `App Configuration Data Reader` for the app identity, per environment.
4. Confirm the **label** values used per environment, since `ASPNETCORE_ENVIRONMENT` must match them.

Note: configuration is read at startup only, as in core-functions. A configuration change therefore
needs an app restart — roughly what app settings do today anyway.

### 6. Octopus variables

Add as **environment-scoped** variables: `AppConfiguration_Uri` and `Azure:TenantId`.
`GraphApiAuth:TenantId` and `GraphApiAuth:ClientId` stay as they are.

Retire `GraphApiAuth:ClientSecret` (project-scoped only, so no blast radius) — and delete the
credential in Entra afterwards, per item 3.

Values that move into App Configuration instead of Octopus: the Serilog/Elastic settings, the
ElasticApm settings, and `GraphApiEmailSettings`.

### 7. The Bicep runbook — run it before item 3

Bind the template's parameters — `acrName`, `acrResourceGroup`, `aspNetCoreEnvironment`,
`appConfigurationUri`, `azureTenantId` — and **run the runbook in dev before the first container
release**. It last ran in 2023, so it is worth treating the first run as a test in its own right.

The run creates `uid-ftpb-email-api` and returns `managedIdentityPrincipalId` and
`managedIdentityClientId` as outputs. The first of those is what item 3 needs.

### 8. The deployment process

Replace the current process with the `ftpb-pdfgenerator` shape:

1. `az webapp config container set --docker-custom-image-name <acr>.azurecr.io/<image>:<tag>`
   (tag = the release version)
2. restart the web app

Remove the `start slot`, `swap` and `stop slot` steps, and delete the `staging` slot in Azure.

### 9. Nothing to rotate — this item is gone

The earlier version of this document asked for an owner for certificate expiry and rotation, under
`AIIDVOI-206`. With a federated identity credential there is no secret and no certificate, so
nothing expires and there is nothing to rotate. That is the risk FT-1999 set out to remove, and this
removes it outright rather than moving it to a new expiry date.

## Open question we are testing ourselves

Does ARM preserve `linuxFxVersion` when the template omits it? If it resets the value instead, a
runbook run would leave the app without an image until the next release, and we will put a required
`imageTag` parameter back. It is a ten-minute test in dev and it is ours to run, not drift's.


## Definition of ready — updated 2026-08-11

Branch:

- [x] B1 — configuration read from Azure App Configuration
- [x] ~~B2~~ — void: the staging slot is gone, so nothing is shared with it
- [x] ~~B3~~ — void for authentication: no certificate, so no Key Vault read on the auth path. Only
      the App Configuration Key Vault references remain, and they may not exist at all
- [ ] B4 — `deploy app` replaced with `az webapp config container set` + restart (drift, no slot involved)
- [x] ~~S1~~ — void: no swap, so no warm-up ping needed
- [x] S2 — `/health` is liveness-only; the Graph check moved to `/health/ready`
- [x] S3 — `az acr build` replaces `docker build`/`docker push`
- [x] S4 — release creation + `develop` / `release/rc-*` / `master` sections in the pipeline
      (house naming, so no branch rename is needed after all)
- [x] S6 — `startCommand` removed
- [x] Graph authentication is a federated identity credential on a user-assigned identity;
      `Azure.Security.KeyVault.Certificates` removed again
- [x] `check-package` run and confirmed for the one dependency that remains new:
      `Microsoft.Extensions.Configuration.AzureAppConfiguration` 8.6.0 (MIT, verified from the
      upstream LICENSE — deps.dev reports it as `non-standard`)

Outside — see the ordered list above, or `FT-1999-status-drift.md` for the short version:

- [ ] ACR created per environment, Octopus SP has RBAC-write in its RG
- [ ] Pipeline authentication to ACR (Bitbucket OIDC federated credential preferred)
- [ ] Runbook params bound and the runbook run in dev — creates the identity
- [ ] Federated identity credential on the app registration, subject = the identity's principal id
- [ ] App Configuration Data Reader, and Key Vault read only if the store uses references
- [ ] Octopus variables added and `ClientSecret` retired and deleted in Entra
- [ ] Deployment process replaced, `staging` slot deleted
