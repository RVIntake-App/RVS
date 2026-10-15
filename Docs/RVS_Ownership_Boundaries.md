# RVS — Ownership Boundaries

**Version:** 1.5 · October 14, 2026 (§4.5 done: `RVS` moved to the `RVIntake-App` GitHub organization; OIDC subjects now carry the org and repo IDs)
**Status:** Action plan. The legal owner is decided (§4.1), the role-alias mail is live (§4.4) and the repo is in its own GitHub organization (§4.5). Everything else is not yet started. The entity structure is a draft pending attorney and CPA review.

The goal: **RV Intake can be handed to a new owner as a single unit.** Every account, tenant, domain, trademark and contract it depends on should belong to RV Intake and nothing else, so that a sale is a handover, not a migration project.

This is cheapest to do now. Prod holds only test tenants, the G-5 wipe already plans a redeploy and recreates all users, and no customer has signed yet. Each item below gets harder once there are paying shops: real users in Auth0, live data in Azure, and signed contracts.

Sources:

- A working session on October 8, 2026 about separating RV Intake from Plover, adapted here to what RV Intake actually runs.
- ADS's entity and brand structure, in the private ADS repo at `ADS/company/entities.md`. It decides who owns RV Intake and under what name. Where the two disagree, ADS wins and this document follows it.

---

## 1. The principle: tenant, not subscription

Azure has two separate hierarchies:

- **Billing:** billing account → subscription → resources. This decides **who pays**.
- **Identity:** every subscription trusts exactly one **Entra tenant**. This decides **who can sign in and what identities exist**.

Moving the billing to a buyer is painless; resources keep running. Moving a subscription to a buyer's **tenant** (a "directory change") is what hurts:

- Every RBAC role assignment is deleted.
- Managed identities must be re-enabled or recreated, and get new principal IDs.
- Key Vaults must be re-pointed to the new tenant and their access re-granted.
- App registrations, service principals and GitHub OIDC federated credentials stay behind.
- Some resource types can't be moved at all.

RV Intake runs on managed identity, Key Vault RBAC, Cosmos and Storage data-plane roles, and GitHub OIDC. A directory change at sale time would mean rebuilding all of that during due diligence.

**So the target is: RV Intake gets its own Entra tenant and its own billing account, with nothing shared with any other product.** A separate subscription inside a shared tenant does not achieve this.

Today's tenant already meets the first half: it holds one subscription, and that subscription holds only RV Intake (§3). So no directory change is needed. The job is to keep it that way and to make sure the tenant isn't administered only by a personal account (§4.6).

The same reasoning applies outside Azure. Auth0, GitHub, SendGrid, Twilio, the domain registrar, Stripe, the bank account and the trademark each need to belong to RV Intake alone.

---

## 2. Target state

```
Arnold Digital Solutions LLC (ADS), a Florida LLC   ← owns everything, signs everything
│
└── d/b/a RV Intake  (Florida fictitious name)
    │
    ├── Trademark "RV Intake", registered to ADS
    ├── Bank account titled "ADS d/b/a RV Intake"
    ├── Stripe: legal name ADS, public name + descriptor "RV Intake"
    │
    ├── Entra tenant: today's tenant, already RV Intake only
    │   ├── Billing account (MCA, in ADS's name and card)
    │   │   └── Subscription: today's RVS subscription, billing moved
    │   │       ├── rg-rvs-staging-westus3
    │   │       └── rg-rvs-prod-westus3   (incl. DNS zones)
    │   ├── Admin + break-glass accounts created IN this tenant
    │   └── Deployer app registrations + federated credentials
    │
    ├── GitHub organization: RV Intake only  →  repo RVS
    ├── Auth0 tenant: today's tenant, obsolete apps deleted
    ├── SendGrid account, Twilio account: RV Intake only
    ├── GoDaddy account holding rvintake.com and its
    │   Microsoft 365 mailbox, nothing else
    │
    └── Every login email is a role alias on rvintake.com
        (§4.4), forwarding to your inbox

Stays with ADS, not part of RV Intake:
    rvserviceflow.com   — "RVS", a marketing-only umbrella name. No legal role, no DBA.
    Plover              — its own LLC soon after launch.
    Future products     — each its own DBA and isolated accounts, on this same pattern.
```

Tenants and subscriptions cost nothing. The ongoing overhead is one more sign-in.

---

## 3. Where RV Intake stands today

| Area | Today | Problem for a handover | Fix (section) |
|---|---|---|---|
| **Legal owner** | Arnold Digital Solutions (ADS) signs agreements, will file the toll-free verification (#659, not yet filed), and is named on the Intake policy pages. | **Decided:** ADS keeps RV Intake. ADS also owns other products, so a sale is an asset sale and every account, contract and the trademark has to be transferable on its own. | §4.1 |
| **Customer-facing name** | No fictitious name registered. Legal pages say "Arnold Digital Solutions LLC, doing business as RV Intake", matching the Pilot Agreement. The toll-free verification (#659) is not filed yet; its Trust Hub profile must use the same name (§4.1). | Trading as RV Intake without a registered DBA. Contracts and invoices need one consistent "ADS d/b/a RV Intake" form. | §4.1 |
| **Customer contract** | **Drafted (v1.4):** the Pilot Agreement names "Arnold Digital Solutions LLC, doing business as RV Intake", adds an assignment clause, and runs the X-3 license to successors and assigns. The `/terms` and `/privacy` pages carry the same successor and transfer wording. All of it still needs attorney review. | Until the attorney confirms the wording, the X-3 data license may not survive a sale, and every contract may need re-signing. | §4.2 — **before Nova accepts** |
| **Trademark** | None filed. | **Decided:** the name and logo stay. The word mark is descriptive, so expect the Supplemental Register at best, after use. A buyer relies on the clearance search and the domain more than on the registration. | §4.3 |
| **Domains** | `rvintake.com` (product) and `rvserviceflow.com` (now the RVS umbrella, stays with ADS). DNS zones are in Azure (good). `rvintake.com` is in a GoDaddy account of its own, with login `godaddy@` (§4.4); its registrant contact still needs ADS's name and address (§4.8). | The API origin `api.rvserviceflow.com`, the Auth0 audience and the claim namespace all sit on a domain RV Intake won't own. Registrant and registrar login for `rvintake.com` must belong to ADS for RV Intake. | §4.4, §4.8, §4.9 |
| **Auth0** | One tenant (#610). It also holds Benefetch, ManagersFriend, RVMTP and other apps, all obsolete and to be deleted. | None once they're gone: the tenant is RV Intake's alone and hands over by changing its admins. The admin login still needs to be the `auth0@` alias. | §4.7 |
| **Azure tenant** | **Confirmed:** one tenant, created with `markarnoldutah@gmail.com`, holding one subscription with only RV Intake resources. | No directory change needed. The open point is admin access: if the personal Microsoft account is the only Global Admin, the tenant can't be handed over cleanly. | §4.6 |
| **Azure billing** | The only subscription in the tenant, so the bill carries nothing but RV Intake. Offer type and billing-account owner: to confirm (§5). Probably an individual account in your name, since the tenant was created with a personal Microsoft account. | A Visual Studio credit subscription can't go to a buyer at all. A pay-as-you-go account in your own name works at sale time, since billing transfers painlessly (§1), but it invoices you rather than ADS. | §4.6 |
| **GitHub** | **Done (§4.5):** `RVIntake-App/RVS`, in an organization owned by `rvintake-gh` (`github@`) and your personal account. OIDC subjects are `repo:RVIntake-App@341154614/RVS@1176179645:environment:*`. | None. A sale adds the buyer as an org owner; the IDs, and so the subjects, don't change. | §4.5 |
| **Ops and DMARC email** | **Done (§4.4):** `ops@rvintake.com` and `dmarc@rvintake.com`, role aliases on the product's own domain. No DMARC records live outside Azure any more. | None. | §4.4 |
| **Customer contact** | `support@arnolddigitalsolutions.com` in `SiteIdentity.cs`, the contact the Twilio toll-free verification (#659) will name. | Same, plus it's tied to the carrier filing. | §4.4 / §4.8 |
| **SendGrid, Twilio** | One account each. Owner login and any sharing with other products: to confirm. | Login emails need to be product aliases, and the accounts must not be shared. | §4.8 |
| **Auth0 identity mail** | Uses its own SendGrid key (Auth0 checklist §8). | Resolves itself if the SendGrid account is RV Intake's own. | §4.7 |
| **Money** | To confirm: which bank account and card pay RV Intake's bills. | Mixed books make the asset sale's purchase-price allocation and due diligence harder. | §4.1 |

Already in good shape: the Azure tenant and subscription hold only RV Intake, Bicep is the only source of truth for Azure, both DNS zones are in Azure, nothing in the repo references a Plover resource, and the Auth0 configuration is scripted (`Docs/ASOT/Infra/Auth0/`).

---

## 4. Action plan, in order

Order matters. The entity sets the names on everything else, and the alias mailboxes have to exist before you create any account with them.

### 4.1 The legal owner: ADS, trading as RV Intake *(decided; attorney + CPA to confirm)*

**ADS owns RV Intake and is the only party that signs.** Customers see **"Arnold Digital Solutions LLC d/b/a RV Intake"**. RV Intake does not get its own LLC, so a sale is an **asset sale**: every account is handed over and every contract and the trademark are assigned. The rest of this document is what makes that workable.

The entity structure, the fictitious-name registration, the dedicated bank account and the order of the Florida filings are business matters and live in the private ADS repo: `ADS/company/entities.md` and `ADS/company/domicile.md` (Phase 3). What stays here is what the product and its accounts have to match:

1. **Write ADS's Florida name and address** on the Azure billing account, the registrar and Stripe once domestication completes. Until then they carry ADS's current details.
2. **In Stripe** (build item 7), the legal name is ADS, and the public business name and statement descriptor are RV Intake.
3. **Keep `SiteIdentity.cs` and the toll-free filing in step.** `LegalEntityName` is "Arnold Digital Solutions LLC", and the pages add "doing business as RV Intake" (Spec A-15), the same form as the Pilot Agreement. When the Twilio Trust Hub profile and the toll-free verification are filed (#659), they must show exactly this name, because carriers check that the site and the filing match. After filing, change them together. Domestication may also change the address on that profile.

The toll-free verification has not been filed yet (#659). File it under ADS with this name, so it never needs refiling.

### 4.2 Customer contract wording *(before the first customer accepts)*

The Pilot Agreement and its wording (contracting party, assignment clause, the X-3 license running to successors and assigns) live in `ADS/products/rv-intake/legal/RVS_Pilot_Agreement.md`, pending the attorney's review.

What stays here: the `/terms` and `/privacy` pages (`RVS.Blazor.Intake/Pages/Terms.razor` and `Privacy.razor`) must carry the same three points wherever they grant rights, in the wording the attorney confirms for the Pilot Agreement.

### 4.3 Trademark "RV Intake" *(attorney)*

**Decided October 13 2026: the name "RV Intake" and the Service Tag logo stay.** The domain work in §4.4 and §4.9 proceeds on `rvintake.com`. "RV Intake" describes the product, so a Principal Register registration is unlikely; the plan is a clearance search with a written opinion, then a use-based Supplemental Register filing in Class 42 once there is use in commerce. The steps are in `ADS/products/rv-intake/README.md` ("Trademark"). Any registration belongs to ADS and is assigned to a buyer at sale (§7).

What touches the product: product copy, the Guides and the legal pages use the name as a brand with a noun ("RV Intake™ service-intake software"), never as the name of the category.

### 4.4 Role-alias mail on `rvintake.com` *(done October 13 2026)*

Every account login and operational address is a role alias on the product's own domain, delivered to one mailbox and forwarded to your inbox. In a sale the domain goes with the product: the buyer changes one forwarding rule and every login email stays the same. Aliases on an ADS-held domain (`arnolddigitalsolutions.com`, `rvserviceflow.com`) would stay with ADS, and each account would then have to be re-verified one at a time.

**What is in place:**

- **Mailbox:** Microsoft 365 Email Essentials from GoDaddy, one mailbox, `support@rvintake.com`. It sits in a GoDaddy account that holds `rvintake.com` and nothing else, so the domain and its mail hand over together. GoDaddy runs it in its own Microsoft tenant, separate from the Azure Entra tenant (§4.6) and from any ADS mail.
- **On the apex, not a subdomain.** This plan put the aliases on a subdomain such as `team.rvintake.com` so the apex could keep its null MX (#608). Microsoft 365 from GoDaddy can't host a subdomain mailbox, so the apex now receives mail. Product email is unaffected: it still goes out from `mail.rvintake.com`.
- **Aliases:** `azure@` · `github@` · `auth0@` · `sendgrid@` · `twilio@` · `godaddy@` · `stripe@` · `uspto@` · `bank@` · `billing@` · `ops@` · `dmarc@`, all delivering to `support@`. They are real aliases, not `you+azure@`, because several services reject plus-addressing. `godaddy@` is the registrar login and replaces the `registrar@` in earlier versions.
- **DNS (Bicep, prod only):** `intakeApexM365MailExchange` points the apex MX at Microsoft 365, sets SPF to `v=spf1 include:spf.protection.outlook.com -all` and adds `autodiscover`. `intakeApexMailVerificationToken` carries Microsoft's `MS=` ownership value. DMARC stays `p=reject; sp=reject`. The details, including why the SPF is narrower than GoDaddy suggests, are in the Bicep README, "Intake apex mail posture".
- **Repo:** `opsAlertEmailReceivers` is `ops@rvintake.com` and `dmarcReportingAddress` is `dmarc@rvintake.com` in both `.bicepparam` files. The reporting address is now in the same domain as the DMARC records, so no RFC 7489 authorization records are needed and the manual step at the ADS registrar is gone.
- **Forwarding:** `support@` forwards everything to your inbox and keeps a copy. GoDaddy's Forwarding switch is not enough on its own: the tenant's **Default** remote domain blocks automatic forwarding, and Exchange then drops the forward without a bounce (`RESOLVER.MSGTYPE.AF` in the message trace). A remote-domain entry for the destination domain alone, with **Allow automatic forwarding** on, lets it through (Exchange admin center → Mail flow → Remote domains). Leave Default blocking. This mailbox receives the password resets for every account, so a forwarding rule planted by an attacker is the quiet way in, and Default dropping it to any other domain is the guard.
- **Tested:** an alias delivers to `support@`; mail sent from `support@` passes SPF and DMARC; a forwarded copy reaches the destination inbox with SPF (rewritten to `rvintake.com`), DKIM, DMARC and ARC passing.

**Still open:**

- **MFA on `support@rvintake.com`.** It can reset every other account, so it needs its own authenticator or passkey (mysignins.microsoft.com → Security info). GoDaddy-run tenants don't always turn it on.
- **DKIM** for `rvintake.com`. GoDaddy's setup didn't offer it. Mail passes DMARC on SPF alone for now; add DKIM in the Microsoft 365 tenant and declare its two CNAMEs in Bicep.
- **`support@` in `SiteIdentity.cs`** stays `support@arnolddigitalsolutions.com` until it changes together with the toll-free filing (§4.1, §4.8).
- **The private account inventory** (§4.8, "Paperwork") records the GoDaddy account, its recovery methods and the setup history in the ADS repo.

### 4.5 Create the GitHub organization and transfer the repo *(done October 14 2026)*

`RVS` lives in the `RVIntake-App` organization, owned by `rvintake-gh` (login `github@`, §4.4) and by your personal account, which is the owner you can remove. Don't put any other product's repos in it. GitHub redirects the old `markarnoldutah/RVS` URL, and issues, PRs and Actions history came along. The environments came through intact: `staging` and `production` each have the four variables, `production` has its two SWA tokens, and the two staging SWA tokens are repository secrets. Either `markarnoldutah` or `rvintake-gh` can approve a staging deploy; production has no required reviewer, since it runs only by manual trigger.

**Only transfer code ADS can sell.** Nothing in the repo may depend on code that can't be licensed or transferred to a buyer, such as a private package shared with another product.

**The OIDC subject carries the org and repo IDs.** The organization's subject template uses immutable IDs, so the subjects are `repo:RVIntake-App@341154614/RVS@1176179645:environment:staging` and `…:environment:production`, both on the one deployer app registration, `github-actions-rvs-deployment` (Option A in `PROD_DEPLOYER_SP_SETUP.md`). A subject without the IDs never matches. The client ID and the GitHub environment variables didn't change. Staging deploy #356 and production deploy #17 both signed in to Azure from the new org.

The IDs survive renaming the org or repo, and a sale hands over the org itself, so the subjects never change at sale. A transfer to a different org would change them and break deploys until each federated credential is edited, in the same sitting.

The repo can stay public.

### 4.6 Secure the Entra tenant and move the billing to ADS

The tenant already holds only RV Intake, so the directory change, the redeploy it would force and the recreated app registrations are all off the plan. What's left:

1. **Admin accounts.** Create a Global Admin *in* the tenant, such as `admin@<tenant>.onmicrosoft.com`, with the `azure@` alias (§4.4) as its contact. Add a second break-glass Global Admin, keep its credentials offline, and turn on MFA for both. Your personal Microsoft account stays as a member you can remove, not the only admin. Rename the tenant's display name to RV Intake. The initial `*.onmicrosoft.com` domain can't be renamed; it is cosmetic.
2. **Billing.** If the subscription is a Visual Studio or MSDN credit subscription (§5), it has to move to pay-as-you-go before anything else. Then create a billing account (Microsoft Customer Agreement) in ADS's name, paid from the RV Intake bank account or card (§4.1), and **transfer billing ownership** of the subscription to it. Resources, names and Azure OpenAI quota stay as they are. Strictly, this is about clean books, not the handover: billing transfers to a buyer from any account type. But it keeps RV Intake's costs off your personal invoices.
3. **Keep the tenant RV Intake-only.** Plover and any other product get their own tenant (§6). Don't add their subscriptions, app registrations or groups here.

**The `rvserviceflow.com` DNS zone** sits in the RVS resource group today. If ADS keeps that domain (§4.9), move its zone to an ADS subscription once nothing of RV Intake's resolves through it.

**When:** any time. None of this needs G-5.

### 4.7 Clear the Auth0 tenant down to RV Intake

The other apps in the tenant (Benefetch, ManagersFriend, RVMTP and the rest) are obsolete. Once they're deleted, the existing tenant is RV Intake's alone, and it hands over the way every other account does: add the buyer as an admin, remove yourself. No new tenant and no reconfiguration.

1. **Delete everything RV Intake doesn't declare.** RV Intake owns what `Docs/ASOT/Infra/Auth0/baseline/` declares (actions, clients, the resource server, roles), plus the `rvs-config-automation` M2M app and the tenant-level settings: the `login.rvintake.com` custom domain, the email provider and Universal Login branding. Delete every other application, API, Action, connection and user. Run `auth0-apply.sh` afterwards: the plan should still report no differences.
2. **Admin login.** Invite the `auth0@` alias (§4.4) as a tenant admin. Your personal login stays as a member you can remove.
3. **Update the docs that describe the shared tenant:** `ASOT/RVS_Identity.md` ("also hosts unrelated products") and `ASOT/Infra/Auth0/README.md`.

The tenant's name, `dev-2jhzz8xmjggh26pm`, can't be changed. Users never see it behind `login.rvintake.com`, and it doesn't stop a handover.

Sharing one tenant across development, staging and production (#610, FS-9) is a separate question and doesn't affect ownership.

### 4.8 Hand the remaining accounts to RV Intake

For each account, the login email becomes the alias, the legal and billing name is ADS (d/b/a RV Intake where the form allows it), and it shares nothing with another product.

| Account | Action |
|---|---|
| **Registrar** (`rvintake.com`) | **Done:** a GoDaddy account of its own, login `godaddy@`, holding `rvintake.com` and its Microsoft 365 mailbox (§4.4). Keep auto-renew on with a card that won't expire soon: if the domain lapsed, the mail that recovers every other account would stop with it. **Still to do:** set the domain's registrant contact to ADS's name, `godaddy@` and ADS's Florida address in one edit after domestication, since each registrant change can start a 60-day transfer lock. `rvserviceflow.com` stays in ADS's own registrar account (§4.9). |
| **SendGrid** | Login `sendgrid@`. If any other product sends through it, give that product its own account. |
| **Twilio** | Login `twilio@`. Toll-free verification is filed per number in a business's name. It is not filed yet (#659); file it under ADS. Keep the Trust Hub profile in step with `SiteIdentity.cs` (§4.1). Moving a number between Twilio accounts goes through Twilio support. |
| **Stripe** (build item 7) | Open it for RV Intake alone from day one: legal name ADS, public business name and statement descriptor "RV Intake", payouts to the RV Intake bank account. Stripe accounts are hard to move between entities. |
| **Bank** | The "ADS d/b/a RV Intake" account (§4.1). Bank accounts don't transfer in an asset sale; what matters is that the books separate cleanly. |
| **USPTO** | Correspondence address `uspto@`. The registration is assigned to the buyer and recorded with the USPTO at sale. |
| **App stores** (if ever) | A developer account for RV Intake alone. Listings are hard to move between accounts. |
| **Anything new** | Same three rules (login alias, ADS's name, nothing shared) from the day it's created. |

Two notes:

- **Personal access:** you keep admin access to everything through your own user, invited as an admin or member. The alias is the account's identity, not your sign-in, so at sale time you remove yourself and nothing breaks.
- **Paperwork:** keep a private inventory of every account, its login alias, its billing owner and its recovery method, in the private ADS repo (under `ADS/products/rv-intake/`), not in this public repo. That inventory is the handover document.

### 4.9 Decide what to do with `rvserviceflow.com` *(open)*

The entity plan keeps RVS as a marketing-only umbrella name, with its domain held by ADS for future use. RV Intake still depends on that domain in three places:

- The API origin, `api.rvserviceflow.com` and `api-staging.rvserviceflow.com`. Browsers call it directly, so it has to resolve.
- The Auth0 audience, `https://api.rvserviceflow.com`.
- The claim namespace, `https://rvserviceflow.com/tenantId` and `/locationIds`, pinned in seven places plus the Auth0 Action.

The last two are opaque strings. A buyer could keep them without owning the domain, though it would look odd in due diligence. The API host is a real dependency. There are two ways out:

- **A. Move the API to `api.rvintake.com` before launch** (the RV Intake Rename Plan). That means a new hostname alongside the old one, then Bicep parameters, the Blazor API base URLs and CORS, and a new Auth0 API with a new audience in the existing tenant. It's roughly an afternoon before launch and grows with every customer after. Then `rvserviceflow.com` is free to stay with ADS.
- **B. Keep the API on `rvserviceflow.com`** and agree now that the domain goes with RV Intake in a sale. That contradicts the entity plan's use of RVS as an umbrella.

**Recommendation:** A, done during G-5. Before go-live G-5 step 8 deletes every test user, so no live token or grant depends on the old audience and claim namespace, and changing them costs nothing. After go-live, CLAUDE.md's rule applies: never change them. If A happens, update CLAUDE.md's "Domains" table and its warning about the three opaque identifiers in the same change.

---

## 5. Confirm the current state first

Already confirmed: one Entra tenant, holding one subscription with only RV Intake resources. Run these for the rest and keep the output. They decide §4.6 steps 1 and 2.

```bash
# Who you are in the tenant
az ad signed-in-user show --query "{upn:userPrincipalName, type:userType}" -o table

# Who holds Global Admin
az rest --method get \
  --url "https://graph.microsoft.com/v1.0/directoryRoles/roleTemplateId=62e90394-69f5-4237-9190-012177145e10/members" \
  --query "value[].{name:displayName, upn:userPrincipalName}" -o table

# Offer type: PayAsYouGo vs MSDN or Visual Studio credit
az rest --method get \
  --url "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)?api-version=2022-12-01" \
  --query "subscriptionPolicies.quotaId" -o tsv

# Billing accounts and their agreement types
az billing account list --query "[].{name:displayName, type:agreementType}" -o table

# Entra objects in the tenant: should all be RV Intake's
az ad app list --query "[].{name:displayName, appId:appId}" -o table
az ad group list --query "[].displayName" -o tsv
```

How to read the results:

- **UPN contains `#EXT#`, or the only Global Admin is your personal account:** do §4.6 step 1 before anything else.
- **quotaId starts with `MSDN` or contains `VisualStudio`:** a personal benefit subscription. It's tied to your subscriber license and can't go to a buyer. Move to pay-as-you-go first.
- **Agreement type is `MicrosoftOnlineServicesProgram`:** an individual pay-as-you-go account in your name. Fine for a handover, but do §4.6 step 2 for the books.
- **An app registration or group belongs to another product:** delete it, or move that product to its own tenant.

---

## 6. Rules from here on

1. **Share nothing between products.** No shared Auth0 tenant, Log Analytics workspace, Key Vault, storage account, DNS zone, SendGrid account, Stripe account, GitHub org or ACR. One shared resource ties two products together. This applies equally to Plover and to any future ADS product: each gets its own DBA, ToS, Stripe descriptor and isolated accounts on the RV Intake pattern.
2. **No shared code that can't be transferred.** If two products need the same code, it is either copied or licensed in a way a buyer can take over.
3. **Bicep stays the only source of truth.** If a tenant transfer ever goes badly, a buyer can still redeploy into their own subscription and copy the data: Cosmos container export, `azcopy` for blobs and tables. That fallback is what keeps this from blocking a sale.
4. **No personal identities as sole owners.** Every account has a role-alias login and ADS on the bill. Your personal user is a member you can remove.
5. **ADS is the only party that signs.** Customers contract with "Arnold Digital Solutions LLC d/b/a RV Intake". RVS never appears as a party.
6. **New accounts follow §4.8 the day they're created.** Adding a service is when a dependency on ADS's other products usually slips in.

---

## 7. At sale time (for reference)

ADS-wide sale rules are in `ADS/company/entities.md`. The account handover:

ADS owns other products, so a sale of RV Intake is an asset sale. If the work above is done, the handover is:

1. **Accounts:**
   - Add the buyer as Global Admin of the Entra tenant and as owner of the billing account.
   - Add the buyer as owner of the GitHub org, as admin of the Auth0, SendGrid and Twilio accounts, and on the registrar account.
   - Hand over the GoDaddy account that holds `rvintake.com` and its mailbox, and point the mailbox's forwarding at the buyer's inbox.
   - In the mailbox's Exchange admin center, replace the `arnolddigitalsolutions.com` remote-domain entry with one for the buyer's domain (automatic forwarding on), or forwarding to them is dropped without a bounce (§4.4). Leave Default blocking.
2. **Legal:**
   - Assign the customer contracts under §4.2's clause.
   - Assign the "RV Intake" trademark and record the assignment with the USPTO.
   - Transfer the `rvintake.com` registration, either with the GoDaddy account or to the buyer's registrar, together with the mailbox.
   - ADS withdraws its RV Intake fictitious name; the buyer files its own.
   - The buyer opens its own Stripe account and bank account. Neither transfers.
3. **Remove yourself everywhere.**
4. **Rotate every secret after you leave:** Key Vault values, the SendGrid and Twilio keys, Auth0 M2M secrets, the SWA deployment tokens, and the GoDaddy and mailbox passwords and passkeys.
