# RVS — Ownership Boundaries

**Version:** 1.3 · October 13, 2026 (Azure tenant confirmed RV Intake-only; the other Auth0 apps are obsolete, so the tenant stays)
**Status:** Action plan. The legal owner is decided (§4.1). Everything else is not yet started. The entity structure is a draft pending attorney and CPA review.

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
    ├── Registrar account holding rvintake.com
    │
    └── Every login email is a role alias on rvintake.com's
        mail subdomain (§4.4), forwarding to your inbox

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
| **Domains** | `rvintake.com` (product) and `rvserviceflow.com` (now the RVS umbrella, stays with ADS). DNS zones are in Azure (good). Registrar account: to confirm. | The API origin `api.rvserviceflow.com`, the Auth0 audience and the claim namespace all sit on a domain RV Intake won't own. Registrant and registrar login for `rvintake.com` must belong to ADS for RV Intake. | §4.4, §4.8, §4.9 |
| **Auth0** | One tenant (#610). It also holds Benefetch, ManagersFriend, RVMTP and other apps, all obsolete and to be deleted. | None once they're gone: the tenant is RV Intake's alone and hands over by changing its admins. The admin login still needs to be the `auth0@` alias. | §4.7 |
| **Azure tenant** | **Confirmed:** one tenant, created with `markarnoldutah@gmail.com`, holding one subscription with only RV Intake resources. | No directory change needed. The open point is admin access: if the personal Microsoft account is the only Global Admin, the tenant can't be handed over cleanly. | §4.6 |
| **Azure billing** | The only subscription in the tenant, so the bill carries nothing but RV Intake. Offer type and billing-account owner: to confirm (§5). Probably an individual account in your name, since the tenant was created with a personal Microsoft account. | A Visual Studio credit subscription can't go to a buyer at all. A pay-as-you-go account in your own name works at sale time, since billing transfers painlessly (§1), but it invoices you rather than ADS. | §4.6 |
| **GitHub** | Personal account `markarnoldutah/RVS`. OIDC subjects are `repo:markarnoldutah/RVS:environment:*`. | You can't hand over a personal account. Moving the repo changes the OIDC subject, which breaks deploys. | §4.5 |
| **Ops and DMARC email** | `ops@` and `support@arnolddigitalsolutions.com` (`prod.bicepparam`). DMARC authorization records sit in the ADS zone at its registrar. | Alerts and reports keep going to an address the buyer can't reach. Part of the DNS lives outside the product. | §4.4 |
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

### 4.4 Set up role-alias mail on an `rvintake.com` subdomain

Every account login and operational address becomes a role alias on the product's own domain, forwarding to your inbox. In a sale the domain goes with the product: the buyer changes one forwarding rule and every login email stays the same. Aliases on any ADS-held domain stay with ADS, and each account would then have to be re-verified one at a time.

**That rules out both `arnolddigitalsolutions.com` and `rvserviceflow.com`.** Version 1.0 of this plan put the aliases on `rvserviceflow.com`. Under the entity plan that domain is the RVS umbrella and stays with ADS, so it would not go to a buyer.

**Use a dedicated mail subdomain of `rvintake.com`**, for example `team.rvintake.com`. The `rvintake.com` apex deliberately accepts no mail (null MX, #608). A subdomain gets its own MX and leaves the apex posture alone.

- **DNS change (Bicep):** add an MX record-set named for the subdomain, pointing at the forwarding provider, through `mxRecords` in `modules/dns.bicep` for the intake zone. Add any verification TXT the provider needs through `txtRecords`. Never add records by hand: an undeclared MX in the `rvserviceflow.com` zone once sat unnoticed until 2026-09-17. Keep the apex null MX and DMARC `p=reject; sp=reject` as they are. They govern mail *sent as* the domain, and these aliases only receive.
- **Provider:** a forwarder that only needs MX records and supports a subdomain, since the zone is in Azure DNS and not Cloudflare. Don't host it in your ADS Workspace or M365: that ties the domain to ADS's mail system.
- **Aliases:** `azure@` · `github@` · `auth0@` · `sendgrid@` · `twilio@` · `registrar@` · `stripe@` · `uspto@` · `bank@` · `billing@` · `ops@` · `dmarc@` · `support@`. Use real aliases, not `you+azure@`; several services reject plus-addressing.
- **Repo changes once the aliases work:**
  - `prod.bicepparam`: `ops@arnolddigitalsolutions.com` → `ops@<subdomain>.rvintake.com`.
  - `dmarcReportingAddress` (both `.bicepparam` files) → `dmarc@<subdomain>.rvintake.com`. `main.bicep` already treats a subdomain of the intake zone as internal (`dmarcReportingIsExternal`), so no RFC 7489 authorization records are needed at all. `dmarcReportAuthorizationAction` comes back empty, and the manual step at the ADS registrar goes away. Update the Bicep README "DMARC aggregate reports" and "Intake apex mail posture" sections to match.
  - `support@` in `SiteIdentity.cs`: change only together with the toll-free filing (§4.1, §4.8).

### 4.5 Create the GitHub organization and transfer the repo

Create an organization owned by `github@` (§4.4), with your personal account as an owner, and transfer `RVS` into it. Don't put any other product's repos in it. GitHub redirects the old URL, and issues, PRs and Actions history come along. After the transfer, check that the `staging` and `production` environments still have their variables and secrets, including both SWA deployment tokens.

**Only transfer code ADS can sell.** Nothing in the repo may depend on code that can't be licensed or transferred to a buyer, such as a private package shared with another product.

**This breaks deploys until the federated credentials are updated**, because the OIDC subject includes the owner. In the same sitting, edit the subject on each federated credential of the two deployer app registrations to `repo:<org>/RVS:environment:*` (`PROD_DEPLOYER_SP_SETUP.md`). The app registrations, client IDs and GitHub environment variables stay as they are. Then update the repo's references to `markarnoldutah/RVS`:

- `.github/workflows/README.md`
- `Docs/ASOT/Infra/Bicep.IaC/PROD_DEPLOYER_SP_SETUP.md`
- `Docs/ASOT/Infra/Bicep.IaC/deployment-cmds.azcli` (`GITHUB_REPO`)
- `.github/agents/github-issue-manager.agent.md`

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
| **Registrar** (`rvintake.com`) | Put it in a registrar account whose login is `registrar@` and that holds no other product's domains. Registrant is ADS. Turn on auto-renew. `rvserviceflow.com` goes in ADS's own registrar account, not this one (§4.9). |
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
   - Point the `rvintake.com` mail-subdomain forwarding at the buyer's inbox.
2. **Legal:**
   - Assign the customer contracts under §4.2's clause.
   - Assign the "RV Intake" trademark and record the assignment with the USPTO.
   - Transfer the `rvintake.com` registration.
   - ADS withdraws its RV Intake fictitious name; the buyer files its own.
   - The buyer opens its own Stripe account and bank account. Neither transfers.
3. **Remove yourself everywhere.**
4. **Rotate every secret after you leave:** Key Vault values, the SendGrid and Twilio keys, Auth0 M2M secrets, and the SWA deployment tokens.
