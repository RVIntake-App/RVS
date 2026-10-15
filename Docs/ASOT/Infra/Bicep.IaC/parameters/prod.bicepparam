using '../main.bicep'

// ──────────────────────────────────────────────────────────────
// PROD — the one and only production parameter file
// ──────────────────────────────────────────────────────────────
// Deployable as committed, first time and every time. There is no
// phase 1 / phase 2 and nothing in here to fill in after the fact.
//
// The single thing Bicep does not do is the one-time registration
// of the rvintake.com apex against the Intake SWA: Azure mints the
// ownership token at registration time, so it cannot be authored
// here. That handshake is a portal (or CLI) step you run once,
// after the first deploy — README.md "Deploy Production", step 2.
// Redeploys never touch the binding. The token itself is then copied
// into intakeApexValidationToken below (#652), because it shares the
// apex TXT record-set with SPF and each deploy re-writes that set.
// ──────────────────────────────────────────────────────────────

param environmentName = 'prod'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-prod-westus3'
param whisperResourceGroupName = 'rg-rvs-prod-ncus'
param openAiCapacity = 30
param whisperCapacity = 2

// Preliminary-assessment-only model, independent of textDeploymentName (#584).
// Blank assessmentModelName and redeploy to revert to gpt-4o with zero
// application-code changes. DataZoneStandard (US) SKU — gpt-5 isn't offered
// under the regional Standard SKU textDeploymentName uses.
param assessmentModelName = 'gpt-5'
// Capacity 30 (30K TPM). A text-only call reserves ~3.6K tokens against TPM up
// front (~1.6K prompt + max_completion_tokens 2000, reasoning included); with
// photos (#772, up to 5 at detail "high", ~0.8-1.1K each) it reserves ~7.6-9.3K,
// so 30 allowed ~3 photo calls a minute. The packet worker sends one at a time,
// so a burst above that queues rather than dropping to text-only. #783 added 10
// so step-6 question calls (~4K each: ~0.6K prompt + max_completion_tokens
// 3000) don't compete with packet assessments. Capacity costs nothing on a
// pay-per-token deployment; the ceiling is quota. Staging + prod share one
// DataZoneStandard gpt-5 quota in westus3: 300K TPM, 30 + 40 = 70K used after
// #783 (20 + 30 checked Sep 25 2026).
param assessmentDeploymentCapacity = 40

// Step-6 diagnostic question generation on the gpt-5 deployment above (#783).
// Moves questions only; category suggestion stays on gpt-4o. false and redeploy
// reverts to gpt-4o, no code change.
param questionsUseAssessmentDeployment = true
param questionsReasoningEffort = 'minimal'

// App Service (API) — Basic B1 for the pilot: no Always On, no deployment slot.
// Upgrade path: 'S1' adds Always On and a staging slot (README "SKU Upgrade Paths").
param deployAppService = true
param appServiceSkuName = 'B1'

// Cosmos DB — Serverless, upgrade to Provisioned via: cosmosCapacityMode = 'Provisioned'
param deployCosmosDb = true
param cosmosCapacityMode = 'Serverless'

// Storage (rvs-attachments container + CORS for SAS uploads)
// Custom domains only — default SWA hostnames intentionally excluded.
param deployStorageAccount = true
param storageAllowSharedKeyAccess = false
param storageCorsOrigins = [
  'https://rvintake.com'
  'https://manager.rvintake.com'
]
// devBlobAccessPrincipalId intentionally unset for prod — the app uses its managed
// identity; humans get blob data access just-in-time (PIM) or via break-glass, never standing.

// Key Vault (RBAC model, API managed identity get + list)
param deployKeyVault = true

// Observability (Log Analytics + Application Insights + /health availability test)
param deployObservability = true
// Off until go-live: billed per run. Turning it on also creates the two alerts
// that read it (#602) — test failing, and telemetry gone dark (pings pass, App
// Insights records nothing). Flip at go-live — Docs/RVS_GoLive_Activities.md (G-3).
// Scale frequency/locations with traffic: README "Turning the availability test on".
param deployAvailabilityTest = false
param availabilityTestFrequencySeconds = 900
param availabilityTestLocations = [
  'us-ca-sjc-azr'
]
// 30 is the lowest the workspace accepts; the first 31 days cost nothing extra.
param logAnalyticsRetentionInDays = 30
// Pre-go-live cap. 0.08 GB/day per environment keeps staging + prod together
// inside the 5 GB/month Log Analytics free grant (per billing account). Past the
// cap, ingestion stops until the daily reset and the #494 alerts go blind — raise
// it (or set '-1') at go-live: Docs/RVS_GoLive_Activities.md (G-2).
param logAnalyticsDailyCapGb = '0.08'

// Ops alert receivers for the packet-pipeline critical alerts (#494). Committed
// here rather than left empty — the Action Groups resource provider does a
// full-replace PUT, so an empty array here deletes any receiver added by hand
// in the portal on every deploy (#639); the portal is not a safe place to set
// this. Real ops alias, replacing the personal address that was a stopgap for
// #639 (#648). On rvintake.com since 2026-10-13, so the alias goes with the product
// (Ownership Boundaries §4.4); it lands in the support@rvintake.com mailbox.
param opsAlertEmailReceivers = [
  {
    name: 'oncall'
    email: 'ops@rvintake.com'
  }
]

// Messaging: email through SendGrid, SMS through Twilio (Oct 2 2026 — ACS removed
// before go-live; it retires Sep 30 2028). Neither is an Azure resource. Keys
// live in Key Vault (SendGrid--ApiKey, Twilio--AccountSid / --ApiKeySid /
// --ApiKeySecret / --AuthToken), set by hand; README "Email — SendGrid" and
// "SMS — Twilio".

// Sending subdomain for all prod email, authenticated in SendGrid.
param mailSendingDomain = 'mail.rvintake.com'
// The three CNAMEs SendGrid domain authentication (automated security) shows for
// mail.rvintake.com, with names relative to rvintake.com, e.g.
//   { name: 'em1234.mail', target: 'u1234.wl.sendgrid.net' }
//   { name: 's1._domainkey.mail', target: 's1.domainkey.u1234.wl.sendgrid.net' }
//   { name: 's2._domainkey.mail', target: 's2.domainkey.u1234.wl.sendgrid.net' }
// Empty until the domain is added in SendGrid; the mailSendingDomainAction
// output says what is left to do.
param sendGridDnsRecords = [
  { name: 'em5357.mail', target: 'u116141940.wl191.sendgrid.net' }
  { name: 's1._domainkey.mail', target: 's1.domainkey.u116141940.wl191.sendgrid.net' }
  { name: 's2._domainkey.mail', target: 's2.domainkey.u116141940.wl191.sendgrid.net' }
]
// DMARC aggregate-report destination (#608): the dmarc@ role alias on the
// rvintake.com apex mailbox (Microsoft 365 from GoDaddy, Ownership Boundaries
// §4.4). It is inside the intake zone, so RFC 7489 §7.1 authorization records are
// not needed and dmarcReportAuthorizationAction comes back empty.
//
// History: dmarc-reports@rvserviceflow.com, whose only MX was a third party's
// placeholder, until 2026-09-17. Then dmarc-reports@rvintake.com, which bounced
// because rvintake.com had no MX. Then support@arnolddigitalsolutions.com, with
// manual authorization records in that zone (#608), until 2026-10-13, when the
// rvintake.com apex got a real mailbox.
param dmarcReportingAddress = 'dmarc@rvintake.com'

// SMS (#661). The prod Twilio subaccount's toll-free number and the Messaging
// Service that holds it. Both empty until they are bought and created in the
// Twilio console. Leave smsEnabled false until Twilio shows the number's
// toll-free verification approved (#659), then flip it and redeploy (G-4).
// The ACS numbers (+18662319618 staging, +18332398230 prod) were released on
// Oct 2 2026 and are gone.
param smsFromPhoneNumber = ''
param twilioMessagingServiceSid = ''
param smsEnabled = false

// Static Web Apps — Free until go-live, to cut cost while prod carries no
// traffic. Set back to 'Standard' at go-live for the SLA:
// Docs/RVS_GoLive_Activities.md (G-1).
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-prod-westus2'
param swaSkuName = 'Free'

// DNS — Manager: CNAME manager.rvintake.com, bound by Bicep (#632).
//       Intake:  ALIAS A record at the rvintake.com apex → Intake SWA, written by
//                Bicep; the apex *binding* is the one-time out-of-band step above.
//       API:     CNAME api.rvintake.com (#900; was api.rvserviceflow.com, #633).
//       rvserviceflow.com is kept as the corporate zone, mail posture only — it
//                stays with ADS (Ownership Boundaries §4.9).
param deployDns = true

// The apex TXT record-set holds this token (from the one-time apex registration)
// plus the apex SPF "v=spf1 -all" (#652). A deploy replaces that whole set, so
// the token has to be written here or the deploy deletes it. It is public, not a
// secret: `dig +short TXT rvintake.com` shows it. Check it against the zone,
// not the SWA: `hostname show --query validationToken` reads blank once the
// apex is Ready.
//   az network dns record-set txt show -g rg-rvs-prod-westus3 -z rvintake.com \
//     -n @ --query "TXTRecords[].value" -o json
// Every value there other than "v=spf1 -all" must be carried here, or the deploy
// deletes it. A what-if that shows only "+ v=spf1 -all" on TXT/@ confirms it.
// If the apex is ever re-registered and Azure mints a new token, update this
// before the next deploy. Blank it and the deploy stops managing the apex TXT
// set: no SPF, and nothing is deleted.
param intakeApexValidationToken = '_gd6d39rcb0u1byedjysur407jqdp2cy'

// Microsoft 365 domain-ownership value for the role-alias mailbox on the
// rvintake.com apex (Ownership Boundaries §4.4), from the GoDaddy Microsoft 365
// setup ("Verify your domain ownership"). Public, not a secret. It joins the apex
// TXT set above, so it is only written while intakeApexValidationToken is set.
param intakeApexMailVerificationToken = 'MS=ms66821433'

// Turns the apex mailbox on: MX → Microsoft 365, SPF include:spf.protection.outlook.com,
// autodiscover CNAME. Blank it to go back to null MX and "v=spf1 -all".
param intakeApexM365MailExchange = 'rvintake-com.mail.protection.outlook.com'

// Grant DNS Zone Contributor (zone-scoped, NOT RG-wide) to the staging
// GitHub Actions service principal so its `staging.bicepparam` deploys can
// upsert CNAMEs in the prod-owned zones. Object IDs only — get with:
//   az ad sp show --id <appId> --query id -o tsv
param dnsZoneContributorPrincipalIds = [
  // Display Name:  github-actions-rvs-deployment
  // APP_ID: de5714f8-924a-4761-b278-9e7a94f2d116
  '9b1e460a-6bed-418a-9d65-1bbed1f768a9'
]
