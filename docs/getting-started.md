# Getting started

This guide gets Harmonee running on your machine, first with **development sign-in** (no setup needed), then with **real accounts** from Microsoft Entra External ID.

---

## 1. Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.400 or later (see `global.json`) | `dotnet --version` |
| SQL Server | LocalDB (installed with Visual Studio) **or** Docker | LocalDB is used by default on Windows. |
| Trusted HTTPS dev certificate | — | `dotnet dev-certs https --trust` (once per machine) |

> **No LocalDB?** Delete the `ConnectionStrings` section from `src/Harmonee.AppHost/appsettings.Development.json` and install Docker: the AppHost then runs SQL Server in a container automatically.

---

## 2. Run it (development sign-in)

```bash
dotnet run --project src/Harmonee.AppHost
```

The Aspire dashboard opens (logs, traces and metrics for every service). The web app is at **https://localhost:7180**.

On first run the migration service creates the `Harmonee` database (one schema per module), then the API and web app start.

Click **Sign in** and choose any display name. The same name always signs in as the same person, so you can test as several people (use a private window for a second person). Tick **Sign in as a platform administrator** to see the admin panel.

> Development sign-in is only allowed in the `Development` environment; the apps refuse to start with it anywhere else. It's controlled by `Harmonee:AuthenticationMode` in `src/Harmonee.AppHost/appsettings.Development.json`.

**If your dev certificate isn't trusted** and the web app shows "Something went wrong" after signing in, either trust it (`dotnet dev-certs https --trust`) or let the web app call the API over local HTTP:

```bash
dotnet run --project src/Harmonee.AppHost -- --Harmonee:ApiBaseAddress=http://api
```

### Try the main flows

1. Create a family. You're its owner; it starts with two roles: **Member** (edit all content) and **Viewer** (read-only, the default for people who join).
2. Open **Members & roles** and copy the invite code.
3. In a private window, sign in as someone else and **Join a family** with the code. They can see everything but change nothing.
4. As the owner, give them the Member role, or create a custom role (for example "Kids": view the calendar, manage shopping lists).
5. Sign in as a platform admin and open **Admin** to find any family. You get full access to it, and every action is audit-logged.

---

## 3. Use real accounts (Microsoft Entra External ID)

You need an **Azure subscription** (External ID's free tier covers the first 50,000 monthly active users) and these registrations:

| What you create | Where | What you copy |
|---|---|---|
| External tenant | Entra admin center | **Tenant subdomain** (e.g. `harmonee` from `harmonee.ciamlogin.com`) and **Tenant ID** |
| App registration "Harmonee API" | External tenant → App registrations | **Application (client) ID** |
| App registration "Harmonee Web" | External tenant → App registrations | **Application (client) ID** and a **client secret value** |
| Sign-up/sign-in user flow | External tenant → External Identities → User flows | — |
| (Optional) Admin role assignment | External tenant → Enterprise apps → Harmonee API | — |

### 3.1 Create the external tenant

1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com).
2. **Entra ID → Overview → Manage tenants → Create**, choose **External**, and link it to your subscription.
3. Switch into the new tenant (settings icon → Directories + subscriptions). From **Overview**, copy the **Tenant ID** and note the **primary domain**: `harmonee.onmicrosoft.com` means the subdomain is `harmonee`.

> Use a **different email** for your test customer account than your tenant admin account (known External ID limitation).

### 3.2 Register the API ("Harmonee API")

1. **App registrations → New registration**: name `Harmonee API`, *Accounts in this organizational directory only*, no redirect URI. Copy the **Application (client) ID**.
2. **Manifest**: set `"requestedAccessTokenVersion": 2` (shown as `accessTokenAcceptedVersion` in older manifest views). **Required.** Without it, token validation fails with `IDX20804`.
3. **Expose an API**: set the Application ID URI to `api://<api-client-id>`, then **Add a scope**:
   - Scope name: `Harmonee.Access`
   - Who can consent: *Admins only*
   - Display name / description: "Access Harmonee on behalf of the signed-in user"
4. **App roles → Create app role**:
   - Display name: `Harmonee administrator`
   - Allowed member types: *Users/Groups*
   - Value: `Harmonee.Admin`. This must match exactly.
5. **Token configuration → Add optional claim → Access token → `email`**, so admins can see who's who.

### 3.3 Register the web app ("Harmonee Web")

1. **App registrations → New registration**: name `Harmonee Web`, *Accounts in this organizational directory only*.
   - Redirect URI: platform **Web**, `https://localhost:7180/signin-oidc`
2. **Authentication**: add `https://localhost:7180/signout-callback-oidc` as a redirect URI and set it as the **Front-channel logout URL**. Leave implicit grant **off**.
3. **Certificates & secrets → New client secret**. Copy the **value** right away (it's shown only once).
4. **API permissions → Add a permission → APIs my organization uses → Harmonee API → Delegated → `Harmonee.Access`**, then **Grant admin consent**.
5. Copy the **Application (client) ID**.

### 3.4 Create the sign-up and sign-in experience

1. **External Identities → User flows → New user flow**: name `SignUpSignIn`, identity provider *Email with password* (or *Email one-time passcode*), and collect the **Display Name** attribute.
2. In the user flow, open **Applications → Add application → Harmonee Web**.

### 3.5 Plug the values in

Store everything in the **AppHost's user secrets**. They stay on your machine, outside the repo:

```bash
dotnet user-secrets set --project src/Harmonee.AppHost "Harmonee:AuthenticationMode" "EntraExternalId"
```

```bash
dotnet user-secrets set --project src/Harmonee.AppHost "Parameters:entra-instance" "https://<tenant-subdomain>.ciamlogin.com/"
```

```bash
dotnet user-secrets set --project src/Harmonee.AppHost "Parameters:entra-tenant-id" "<tenant-id>"
```

```bash
dotnet user-secrets set --project src/Harmonee.AppHost "Parameters:api-client-id" "<api-client-id>"
```

```bash
dotnet user-secrets set --project src/Harmonee.AppHost "Parameters:web-client-id" "<web-client-id>"
```

```bash
dotnet user-secrets set --project src/Harmonee.AppHost "Parameters:web-client-secret" "<web-client-secret-value>"
```

Run the AppHost again, click **Sign in**, and sign up with a new account.

### 3.6 Make yourself a platform administrator (optional)

After signing up once (so your user exists):

1. **Enterprise apps → Harmonee API → Users and groups → Add user/group**.
2. Select your user, select the role **Harmonee administrator**, and click **Assign**.
3. Sign out of Harmonee and sign in again. **Admin** appears in the header.

---

## 4. Tests

```bash
dotnet test --solution Harmonee.slnx
```

- **Integration tests** run the real API against a real SQL Server database (LocalDB by default, or set `HARMONEE_TEST_SQL` to a server connection string). Each run creates and drops its own database.
- The **OpenAPI contract test** fails when the API surface changes. If the change is intended, accept it with `HARMONEE_UPDATE_SNAPSHOTS=1 dotnet test --solution Harmonee.slnx` and commit the updated snapshot.

---

## 5. Troubleshooting

| Symptom | Fix |
|---|---|
| `IDX20804: Unable to retrieve document ... /common/discovery/keys` | Set `requestedAccessTokenVersion` to `2` in the **API** app manifest (§3.2). |
| `AADSTS50011` redirect URI mismatch | The web app's redirect URI must be exactly `https://localhost:7180/signin-oidc`. |
| `AADSTS65001` consent required | Grant admin consent for `Harmonee.Access` on the web app (§3.3 step 4). |
| Signed in but every page says "You don't have permission" | The token is missing the `Harmonee.Access` scope. Check §3.3 step 4 and the `api-client-id` parameter. |
| "Something went wrong" right after signing in (development mode) | The dev certificate isn't trusted. See §2. |
| Signed out unexpectedly after restarting the app | Expected in local development: the token cache is in memory. Sign in again. Set `ConnectionStrings:cache` (Redis) on the web app to keep it. |
