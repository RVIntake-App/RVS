# RVS — Ownership Boundaries

**Version:** 1.1 · October 11, 2026
**Status:** Action plan. The legal owner is decided (§4.1). Everything else is not yet started. The entity structure is a draft pending attorney and CPA review.

The goal: **RV Intake can be handed to a new owner as a single unit.** Every account, tenant, domain, trademark and contract it depends on should belong to RV Intake and nothing else, so that a sale is a handover, not a migration project.

This is cheapest to do now. Prod holds only test tenants, the G-5 wipe already plans a redeploy and recreates all users, and no customer has signed yet. Each item below gets harder once there are paying shops: real users in Auth0, live data in Azure, and signed contracts.

Sources:

- A working session on October 8, 2026 about separating RV Intake from RigRoom, adapted here to what RV Intake actually runs.
- The **ADS Entity & Brand Structure** plan (October 2026). It decides who owns RV Intake and under what name. Where the two disagree, the entity plan wins and this document follows it.

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
    ├── Entra tenant: RV Intake only
    │   ├── Billing account (MCA, in ADS's name and card)
    │   │   └── Subscription: today's RVS subscription, moved in
    │   │       ├── rg-rvs-staging-westus3
    │   │       └── rg-rvs-prod-westus3   (incl. DNS zones)
    │   ├── Admin + break-glass accounts created IN this tenant
    │   └── Deployer app registrations + federated credentials
    │
    ├── GitHub organization: RV Intake only  →  repo RVS
    ├── Auth0 account + tenant: RV Intake only
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
| **Legal owner** | Arnold Digital Solutions (ADS) signs agreements, files toll-free verification (#680), and appears in the Intake footer. | **Decided:** ADS keeps RV Intake. ADS also owns other products, so a sale is an asset sale and every account, contract and the trademark has to be transferable on its own. | §4.1 |
| **Customer-facing name** | No fictitious name registered. Legal pages say "Arnold Digital Solutions"; the toll-free filing says "Arnold Digital Solutions (doing business as RV Intake)". | Trading as RV Intake without a registered DBA. Contracts and invoices need one consistent "ADS d/b/a RV Intake" form. | §4.1 |
| **Customer contract** | **Drafted (v1.2):** the Pilot Agreement names "Arnold Digital Solutions LLC, doing business as RV Intake", adds an assignment clause, and runs the X-3 license to successors and assigns. The `/terms` and `/privacy` pages carry the same successor and transfer wording. All of it still needs attorney review. | Until the attorney confirms the wording, the X-3 data license may not survive a sale, and every contract may need re-signing. | §4.2 — **before Nova signs** |
| **Trademark** | None filed. | The name is the main transferable asset after the code. Unregistered, a buyer gets only common-law rights. | §4.3 |
| **Domains** | `rvintake.com` (product) and `rvserviceflow.com` (now the RVS umbrella, stays with ADS). DNS zones are in Azure (good). Registrar account: to confirm. | The API origin `api.rvserviceflow.com`, the Auth0 audience and the claim namespace all sit on a domain RV Intake won't own. Registrant and registrar login for `rvintake.com` must belong to ADS for RV Intake. | §4.4, §4.8, §4.9 |
| **Auth0** | One tenant shared with Benefetch, ManagersFriend, RVMTP and others (#610). | The tenant can't be handed over. Moving users after go-live means a password-hash export through Auth0 support, or forcing every user to reset. | §4.7 — **fold into G-5** |
| **Azure tenant** | To confirm (§5). Probably a tenant that also holds RigRoom, or a personal directory. | Directory change at sale time (§1). | §4.6 |
| **Azure billing** | To confirm (§5). | If it's a personal pay-as-you-go account or a Visual Studio credit subscription, it can't stay with the product. | §4.6 |
| **GitHub** | Personal account `markarnoldutah/RVS`. OIDC subjects are `repo:markarnoldutah/RVS:environment:*`. | You can't hand over a personal account. Moving the repo changes the OIDC subject, which breaks deploys. | §4.5 — do with §4.6 |
| **Ops and DMARC email** | `ops@` and `support@arnolddigitalsolutions.com` (`prod.bicepparam`). DMARC authorization records sit in the ADS zone at its registrar. | Alerts and reports keep going to an address the buyer can't reach. Part of the DNS lives outside the product. | §4.4 |
| **Customer contact** | `support@arnolddigitalsolutions.com` in `SiteIdentity.cs`, matching the Twilio toll-free filing. | Same, plus it's tied to the carrier filing. | §4.4 / §4.8 |
| **SendGrid, Twilio** | One account each. Owner login and any sharing with other products: to confirm. | Login emails need to be product aliases, and the accounts must not be shared. | §4.8 |
| **Auth0 identity mail** | Uses its own SendGrid key (Auth0 checklist §8). | Resolves itself if the SendGrid account is RV Intake's own. | §4.7 |
| **Money** | To confirm: which bank account and card pay RV Intake's bills. | Mixed books make the asset sale's purchase-price allocation and due diligence harder. | §4.1 |

Already in good shape: Bicep is the only source of truth for Azure, both DNS zones are in Azure, nothing in the repo references a RigRoom or Plover resource, and the Auth0 configuration is scripted (`Docs/ASOT/Infra/Auth0/`).

---

## 4. Action plan, in order

Order matters. The entity sets the names on everything else, and the alias mailboxes have to exist before you create any account with them.

### 4.1 The legal owner: ADS, trading as RV Intake *(decided; attorney + CPA to confirm)*

**ADS owns RV Intake and is the only party that signs.** Customers see **"Arnold Digital Solutions LLC d/b/a RV Intake"**. ADS is being domesticated to Florida, and all legal filings come from there. RV Intake does not get its own LLC. The entity plan reserves a separate LLC for Plover, not for RV Intake.

That makes a sale an **asset sale**: every account is handed over and every contract and the trademark are assigned. The rest of this document is what makes that workable. On a sale, ADS withdraws its RV Intake fictitious name and the buyer files its own.

To do:

1. **Register the fictitious name** "RV Intake" for ADS on Sunbiz once domestication completes: $50 plus one newspaper ad in the county of ADS's Florida principal place of business. It renews every 5 years.
2. **Open a dedicated bank account** titled "ADS d/b/a RV Intake", and pay every RV Intake bill from it or from a card on it. A bookkeeping class per product is the fallback if the CPA prefers it.
3. **Use the same name form everywhere:** ToS, privacy policy, texting terms, Pilot Agreement, invoices. In Stripe (build item 7), the legal name is ADS, and the public business name and statement descriptor are RV Intake.
4. **Write ADS's Florida name and address** on the Azure billing account, the registrar and Stripe from this point on.
5. **Keep `SiteIdentity.cs` and the toll-free filing in step.** `LegalEntityName` is "Arnold Digital Solutions" today, without "LLC" or the DBA (Spec A-15). If the legal pages change to "Arnold Digital Solutions LLC d/b/a RV Intake", update the Twilio Trust Hub profile in the same change, because carriers check that they match. Domestication may also change the address on that profile.

The toll-free verification is already filed under ADS (#680), so it doesn't need to be refiled.

### 4.2 Fix the Pilot Agreement *(before the first signature)*

`Docs/Legal/RVS_Pilot_Agreement.md`. Have the attorney confirm the exact wording.

1. **Name the legal entity as the party:** "Arnold Digital Solutions LLC, doing business as RV Intake", not "RV ServiceFlow". RV ServiceFlow isn't a legal person, and under the entity plan RVS is a marketing name only.
2. **Add an assignment clause:** ADS may assign this Agreement without consent to an affiliate or to a successor in a merger, acquisition or sale of all or substantially all of the RV Intake business or assets. Say "the RV Intake business" rather than "ADS's business": ADS owns other products, and a sale of RV Intake alone has to count.
3. **Make the X-3 data license run to successors:** "…to Arnold Digital Solutions LLC and its successors and assigns, perpetual and irrevocable…". Grant the license to the entity, not to "RV Intake": a DBA can't hold rights. The Overview calls this license *the one thing that must not be dropped*. Without this wording it may not carry over to a buyer.

The same three points apply to the ToS (`/terms`) wherever it grants rights.

### 4.3 Trademark "RV Intake" *(attorney)*

The entity plan sets the steps. In short:

1. Run a knockout search on the USPTO trademark search system for "RV Intake" and close variants.
2. **Discuss descriptiveness with the attorney first.** "RV Intake" describes RV service-intake software, so it risks a merely-descriptive refusal. The options are the Supplemental Register, a stylized or logo mark (the "Service Tag" mark), or a more distinctive name before launch. A name change is far cheaper now than after the domain work in §4.4 and §4.9.
3. File in **ADS's name**, after the Florida domestication completes so the applicant details are final, in Class 42 (SaaS), and possibly Class 9.
4. Use intent-to-use until there are paying customers, then use-based. Use ™ now and ® only after registration. Check current USPTO fees, which are charged per class.

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

**This breaks deploys until the federated credentials are updated**, because the OIDC subject includes the owner. Do it in the same sitting as §4.6, which recreates those credentials anyway. Then update the repo's references to `markarnoldutah/RVS`:

- `.github/workflows/README.md`
- `Docs/ASOT/Infra/Bicep.IaC/PROD_DEPLOYER_SP_SETUP.md`
- `Docs/ASOT/Infra/Bicep.IaC/deployment-cmds.azcli` (`GITHUB_REPO`)
- `.github/agents/github-issue-manager.agent.md`

The repo can stay public.

### 4.6 Give RV Intake its own Entra tenant and billing account

1. **Create the tenant** while signed in to Azure. Make its first admin an Entra account created *in* that tenant, such as `admin@<tenant>.onmicrosoft.com`, with the `azure@` alias as its contact. Don't use a personal Microsoft account. Add a second break-glass Global Admin, keep its credentials offline, and turn on MFA for both.
2. **Create a billing account** (Microsoft Customer Agreement) in ADS's name, paid from the RV Intake bank account or card (§4.1), and **transfer billing ownership** of the RVS subscription to it. Resources keep running.
3. **Move the subscription into the new tenant (directory change).** First:
   - Run the ACS teardown (`deployment-cmds.azcli` §4e) so there are fewer resource types to move.
   - Check every resource type left against Microsoft's current list of what a directory change breaks: "Transfer an Azure subscription to a different Microsoft Entra directory" on Learn.
4. **Redeploy `main.bicep` for staging and prod.** Bicep recreates most of what the move deleted:
   - Role assignments, including Cosmos and Storage data-plane roles, for the new managed-identity principals.
   - The Key Vault tenant, which is `subscription().tenantId`.
   - System-assigned identities.
5. **Recreate by hand what isn't in Bicep:**
   - The two deployer app registrations and their federated credentials, with the new org subjects from §4.5 (`PROD_DEPLOYER_SP_SETUP.md`).
   - `AZURE_TENANT_ID` and `AZURE_CLIENT_ID` in both GitHub environments.
   - The dev Entra group behind `devBlobAccessPrincipalId`.
   - The object IDs in `dnsZoneContributorPrincipalIds`.
   - Your own `az login --tenant <new>`.
6. **Smoke-test both environments:** a staging deploy from `main`, one intake, the packet email, and a Manager sign-in.

**Why move the subscription instead of building a new one:** a fresh subscription would need new names, because Key Vault purge protection holds a deleted vault's name, and storage, Cosmos and app names are globally unique. It would also need new Azure OpenAI quota. Today gpt-5 and Whisper have zero headroom, and new quota isn't guaranteed. On top of that it would need DNS re-delegation at the registrar and re-validated custom domains. The directory change keeps all of that, and with Bicep and no live customers, the cleanup it causes is one redeploy.

**The `rvserviceflow.com` DNS zone** sits in the same resource group today. Until §4.9 is settled it moves with the subscription. If ADS keeps that domain, move its zone out to an ADS subscription once nothing of RV Intake's resolves through it.

**When:** together with G-5. G-5 already redeploys `main.bicep` and recreates everything, so the extra cost of the move is small.

### 4.7 Give RV Intake its own Auth0 tenant *(fold into G-5)*

The shared tenant (#610) is the hardest thing to separate after go-live, because that's where users and their passwords live. Before go-live, nothing needs migrating: G-5 step 8 already deletes every test user, and `/admin` provisioning recreates Nova's users.

1. **Sign up for a new Auth0 account** with the `auth0@` alias and create one tenant for RV Intake. #610 deferred a second tenant because it needed a paid plan *on the existing account*. Check Auth0's current terms: a separate account may get its own free tenant.
2. **Recreate the configuration from the baseline**, so nothing is set up by hand:
   - Create a new `rvs-config-automation` M2M app with the same scopes.
   - Run `auth0-apply.sh` against the new tenant.
   - Decide the API identifier and claim namespace first (§4.9). They are opaque identifiers, valid in any tenant, and today they are `https://api.rvserviceflow.com` and `https://rvserviceflow.com/…`. A new, empty tenant is the one moment changing them costs nothing, because no token or grant exists yet. After go-live, CLAUDE.md's rule applies: never change them.
3. **Update the secrets and settings:**
   - The `Auth0--*` secrets in both Key Vaults.
   - The `Auth0Mgmt--*` secrets.
   - The Manager's Auth0 domain and client ID.
   - Auth0's identity-mail SendGrid key, now from RV Intake's own SendGrid account.
   - Universal Login branding, from `RvsBrand.cs`.
4. **Do it before G-5 step 8**, so Nova's users are created in the new tenant. Leave RV Intake's apps in the old tenant until staging works against the new one, then delete them there.

### 4.8 Hand the remaining accounts to RV Intake

For each account, the login email becomes the alias, the legal and billing name is ADS (d/b/a RV Intake where the form allows it), and it shares nothing with another product.

| Account | Action |
|---|---|
| **Registrar** (`rvintake.com`) | Put it in a registrar account whose login is `registrar@` and that holds no other product's domains. Registrant is ADS. Turn on auto-renew. `rvserviceflow.com` goes in ADS's own registrar account, not this one (§4.9). |
| **SendGrid** | Login `sendgrid@`. If any other product sends through it, give that product its own account. |
| **Twilio** | Login `twilio@`. Toll-free verification is filed per number in a business's name, and it is already filed under ADS (#680). Keep the Trust Hub profile in step with `SiteIdentity.cs` (§4.1). Moving a number between Twilio accounts goes through Twilio support. |
| **Stripe** (build item 7) | Open it for RV Intake alone from day one: legal name ADS, public business name and statement descriptor "RV Intake", payouts to the RV Intake bank account. Stripe accounts are hard to move between entities. |
| **Bank** | The "ADS d/b/a RV Intake" account (§4.1). Bank accounts don't transfer in an asset sale; what matters is that the books separate cleanly. |
| **USPTO** | Correspondence address `uspto@`. The registration is assigned to the buyer and recorded with the USPTO at sale. |
| **App stores** (if ever) | A developer account for RV Intake alone. Listings are hard to move between accounts. |
| **Anything new** | Same three rules (login alias, ADS's name, nothing shared) from the day it's created. |

Two notes:

- **Personal access:** you keep admin access to everything through your own user, invited as an admin or member. The alias is the account's identity, not your sign-in, so at sale time you remove yourself and nothing breaks.
- **Paperwork:** keep a private inventory of every account, its login alias, its billing owner and its recovery method. Don't keep it in this public repo. That inventory is the handover document.

### 4.9 Decide what to do with `rvserviceflow.com` *(open)*

The entity plan keeps RVS as a marketing-only umbrella name, with its domain held by ADS for future use. RV Intake still depends on that domain in three places:

- The API origin, `api.rvserviceflow.com` and `api-staging.rvserviceflow.com`. Browsers call it directly, so it has to resolve.
- The Auth0 audience, `https://api.rvserviceflow.com`.
- The claim namespace, `https://rvserviceflow.com/tenantId` and `/locationIds`, pinned in seven places plus the Auth0 Action.

The last two are opaque strings. A buyer could keep them without owning the domain, though it would look odd in due diligence. The API host is a real dependency. There are two ways out:

- **A. Move the API to `api.rvintake.com` before launch** (the RV Intake Rename Plan). That means a new hostname alongside the old one, then Bicep parameters, the Blazor API base URLs and CORS, and a new Auth0 API with a new audience, done in the new tenant (§4.7). It's roughly an afternoon before launch and grows with every customer after. Then `rvserviceflow.com` is free to stay with ADS.
- **B. Keep the API on `rvserviceflow.com`** and agree now that the domain goes with RV Intake in a sale. That contradicts the entity plan's use of RVS as an umbrella.

**Recommendation:** A, done during G-5 together with §4.6 and §4.7, since all three already touch the same settings. If A happens, update CLAUDE.md's "Domains" table and its warning about the three opaque identifiers in the same change.

---

## 5. Confirm the current state first

Run these and keep the output. It decides which parts of §4.6 apply.

```bash
# Which tenant the RVS subscription trusts, and who you are in it
az account show --query "{sub:name, tenant:tenantId, user:user.name}" -o table
az ad signed-in-user show --query "{upn:userPrincipalName, type:userType}" -o table

# Every subscription you can see, and its tenant. Does RigRoom share RVS's tenant?
az account list --query "[].{name:name, tenant:tenantId}" -o table

# Every tenant you belong to
az rest --method get --url "https://management.azure.com/tenants?api-version=2022-12-01" \
  --query "value[].{id:tenantId, name:displayName, domain:defaultDomain}" -o table

# Offer type: PayAsYouGo vs MSDN or Visual Studio credit
az rest --method get \
  --url "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)?api-version=2022-12-01" \
  --query "subscriptionPolicies.quotaId" -o tsv

# Billing accounts and their agreement types
az billing account list --query "[].{name:displayName, type:agreementType}" -o table

# Resource types to check against the directory-change list
az resource list --query "[].type" -o tsv | sort | uniq -c
```

How to read the results:

- **UPN contains `#EXT#`:** you sign in with a personal Microsoft account. That's fine for you, but it can't be the product's only admin.
- **quotaId starts with `MSDN` or contains `VisualStudio`:** a personal benefit subscription. It's tied to your subscriber license and can't go to a buyer. Move to a pay-as-you-go or MCA subscription under the billing account.
- **The tenant also holds RigRoom or Plover subscriptions, or other products' identities:** §4.6 is required.
- **The tenant holds only RV Intake already:** skip the directory change. Do step 1's admin accounts and step 2's billing move only.

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
