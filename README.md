- [SolidFire Windows Admin Center (WAC) Gateway](#solidfire-windows-admin-center-wac-gateway)
  - [Objectives](#objectives)
  - [Quick start](#quick-start)
  - [How do authentication and authorization work?](#how-do-authentication-and-authorization-work)
    - [How Gateway action roles map to SolidFire API methods](#how-gateway-action-roles-map-to-solidfire-api-methods)
  - [Key differences between SolidFire WAC Gateway (SWG) and SolidFire API](#key-differences-between-solidfire-wac-gateway-swg-and-solidfire-api)
  - [Accessing SolidFire Gateway API](#accessing-solidfire-gateway-api)
    - [Get Cluster Info: GET `/SolidFire/{cluster}/clusterinfo`](#get-cluster-info-get-solidfireclusterclusterinfo)
    - [Access API from PowerShell](#access-api-from-powershell)
    - [List Volumes for Account: GET `/SolidFire/{cluster}/listvolumesforaccount?accountID={id}`](#list-volumes-for-account-get-solidfireclusterlistvolumesforaccountaccountidid)
    - [Workflow: use SolidFire WAC Gateway to provision storage to Hyper-V cluster](#workflow-use-solidfire-wac-gateway-to-provision-storage-to-hyper-v-cluster)
  - [FAQ](#faq)
    - [Do I need Windows Admin Center (WAC) to use this Gateway?](#do-i-need-windows-admin-center-wac-to-use-this-gateway)
    - [Is there a SolidFire WAC Extension?](#is-there-a-solidfire-wac-extension)
    - [What to use to list volumes for account?](#what-to-use-to-list-volumes-for-account)
    - [How to test volume access control (VolumeAccess.ActionRoles)](#how-to-test-volume-access-control-volumeaccessactionroles)
    - [What account to use to test](#what-account-to-use-to-test)
    - [How to implement HA for SolidFire WAC Gateway?](#how-to-implement-ha-for-solidfire-wac-gateway)
    - [Which browsers and clients can be used?](#which-browsers-and-clients-can-be-used)
    - [Where should SolidFire WAC Gateway be deployed?](#where-should-solidfire-wac-gateway-be-deployed)
    - [Multi-tenancy of SolidFire WAC Gateway](#multi-tenancy-of-solidfire-wac-gateway)
    - [How to use VAGs with SolidFire WAC Gateway?](#how-to-use-vags-with-solidfire-wac-gateway)
    - [What are hidden volume attributes?](#what-are-hidden-volume-attributes)
    - [Can I use SolidFire WAC to achieve Quantum Computing-resistant encryption?](#can-i-use-solidfire-wac-to-achieve-quantum-computing-resistant-encryption)
  - [Security-related notes](#security-related-notes)
    - [Restrict access to certain IPs](#restrict-access-to-certain-ips)
    - [IIS and Application logs](#iis-and-application-logs)
    - [Protecting back-end (SolidFire) credentials](#protecting-back-end-solidfire-credentials)
  - [Development](#development)
    - [Additional notes](#additional-notes)


# SolidFire Windows Admin Center (WAC) Gateway

Minimal Windows Admin Center-focused gateway for NetApp SolidFire with the following features:

- "Universal" API gateway (originally built with WAC in mind)
- Windows authentication and authorization
- Built and tested for IIS on Windows

Use it with Windows Admin Center (WAC) server or from any RESTful client.

- PowerShell 7
- Any other client that can authenticate against Windows using Active Directory and make RESTful calls

SolidFire WAC Gateway works with NetApp SolidFire 12. It doesn't expose the new API methods from 12.8, but those are very "storage admin-centric" anyway and not required on tenant-facing endpoints.

See the rest of this page (especially Security and FAQs) for additional details.

## Objectives

![SolidFire WAC Gateway](/images/solidfire-wac-gateway-diagram.png)

- (1) Implement proper security in front of native SolidFire cluster API
- (2) Implement usable multi-tenancy for Windows (including Hyper-V) users
- (3) Create a WAC-ready API gateway for anyone who wishes to create a secure, multi-tenant-suitable WAC extension for SolidFire
- (4) Implement effective multi-tenancy for power users and "minimal scope" "account-level administrators"

You may read more about the background of this project in my blog posts (linked in FAQs below).

SolidFire WAC Gateway is licensed under a very permissive OSS license, so you may change it any way you see fit and keep those changes to yourself.


## Quick start

I use Git's Bash client here:

1. **Create a directory for projects**
   ```bash
   mkdir "C:\$(whoami)\Projects\"
   ```

2. **Clone the repository to project directory**
   ```bash
   mkdir "\C:\Users\$(whoami)\Projects\SolidFireGateway"
   git clone https://github.com/scaleoutsean/solidfire-wac-gateway "\C:\Users\$(whoami)\Projects\SolidFireGateway"
   ```   

3. **Edit application configuration**
   - Open `appsettings.json` and set your cluster endpoints, credentials, and allowed roles as needed

4. **Choose an unused port for the gateway, create DNS entry, configure IIS site SolidFireGateway**
   - Create DNS entry for the IIS server, use CA to create a TLS and configure it on IIS for the API endpoint
   - Default port is `5000`, but you can use another (refer to `web.config`)
   - Install .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0) and dependencies, then  build:
   ```pwsh
   # dotnet clean .\SolidFireGateway.csproj
   dotnet restore 
   # Remove-Item -Recurse -Force .\bin, .\obj
   dotnet build
   ```
   - Publish to ISS `dotnet publish -c Release -o C:\inetpub\SolidFireGateway` (see `web.config` for IIS options)

`dotnet restore` pulls:

- Microsoft.AspNetCore.Authentication.Negotiate
- Microsoft.AspNetCore.OpenApi
- Swashbuckle.AspNetCore
- Microsoft.AspNetCore.Mvc.NewtonsoftJson


## How do authentication and authorization work?

- Authentication (authN) is Windows authentication through IIS. Anonymous access is disabled on the IIS Web site
- Authorization (authZ)
  - Limited cluster-wide resources (cluster-wide stats, cluster info, QoS policies, etc.) are **not** account-scoped, they're *cluster* resources. All (whitelisted) authenticated domain groups and users from `appsettings.json` can access them. 
  - All APIs that do accept an accountID enforce `TenantOptions.AllowedTenants` (list of Account IDs), so if your DOMAIN\HR team runs VI on a Hyper-V cluster that consumes SolidFire storage accountID `4`, anything to do with account ID 4 (volumes, snapshots, VAGs) requires some account (e.g. DOMAIN\HRIT) or user(s) allowed to access these to be listed in `appsettings.json`. These are quite granular, so knock yourself out.
  - In `appsettings.json`, any user in one of `GlobalAdminRoles` (e.g. DOMAIN\SFADMINS) can still call `/SolidFire/{cluster}/listvolumesforaccount?accountID={any}` and list volumes for **any** tenant. That's about the only "tenant-related" thing that's possible, but you can have that global group empty and eliminate that option if you want. The idea here is that some cross-org cooperation and collaboration may be useful, so letting global admins list those volumes would be harmless. As an example, it may be useful to know if some other team's volumes are paired for replication, what QoS settings volumes that belong to others use, etc.
  - In tenant-related resources, everyone is restricted to only the ID (or IDs) in `TenantOptions.AllowedTenants` (in my development I used just one, so this is `[4]`). And on top of that, there are different actions (API methods) that can be used to micro-manage that. As an example: DOMAIN\SFADMINS may be able to `PUT` (i.e. `ModifyQoSPolicy`), but DOMAIN\sean may be able to only `GET` which is necessary for his Hyper-V admin work (to be able to see what QoS policies exist on the cluster)

Additionally:

- `appsettings.json`, as it is in this repository, contains `"BUILTIN\\Administrators"` because its convenient for testing, but for production you'd likely want to remove that account and add dedicated domain group(s) or account(s). 
- For production, secure the Web/API server and restrict IP access to trusted clients.
- See `appsettings.json` for authorization and back-end (SolidFire) configuration. The TLS configuration option for SolidFire is for SolidFire API endpoints - not for front-end gateway (IIS).
- Since tenant-scoping for `listvolumesforaccount` makes this endpoint (action) accessible to `GlobalAdminRoles` (they can call `GET /SolidFire/{cluster}/listvolumesforaccount?accountID={any}`). You an relax this specific restriction by removing this part in VolumesController.cs which would allow any authorized user (not just `GlobalAdminRoles`) to list volumes that belong to other accounts (and nothing more than that).

```csharp
if (!_tenantOptions.AllowedTenants.Contains(accountID))
{
    return Forbid();
}
```


### How Gateway action roles map to SolidFire API methods

Any HTTP PUT that calls SolidFire `Modify` methods (e.g. `ModifyVolume`) uses the Update role on Gateway. In `VolumeAccess:ActionRoles` section that is the `Update[]` (list). For reference:

- GET collection endpoints -> List
- GET single-item endpoints -> Get
- POST create endpoints -> Create
- PUT modify endpoints -> Update
- DELETE delete endpoints -> Delete

Special purge endpoint is `Purge`, only applicable to Volumes.

As far as Accounts are concerned, `AccountAccess` settings impact these endpoints in `AccountsController` which reference `AccountAccess:ActionRoles`.

- GetAccountEfficiency (action = "Get")
- ListVolumeStats (action = "List")

This is easier to understand. For example:

- `List` applies to collection‐returning endpoints (e.g. `GET /volumes/foraccount`, `GET /snapshots/list`, etc.) which tends to call `List` actions on SolidFire
- `Get` applies to "list by specific ID” methods (e.g. `GET /clusterstats/capacity`, `GET /qospolicies/{id}`, `GET /accounts/efficiency`, etc) - same as Get-by-ID in SolidFire API
- They're almost the same, but `Get` allows us to create clients with volume-specific methods. These could be collapsed into one role (`List`) if no distinction needs to be made between the two


## Key differences between SolidFire WAC Gateway (SWG) and SolidFire API

- Narrow scope: SWG's first priority is to enable multi-tenancy. You can't view, let alone manage, cluster nodes, tenant accounts, networks or anything of that nature
- Safer multi-tenancy: SWG fixes some of the worst parts of SolidFire API
  - Tenant can see his volumes and no other
  - Tenant can see his snapshots and no other
  - Tenant can see VAGs that belong to him and no other. Note: VAGs aren't recommended, CHAP is. If you want to use VAGs, see the FAQs
  - Tenant can't clone volumes that belong to others (for time being I haven't even implemented cloning as I'd rather leave this to "power administrators"; the reason being that clones are usually assigned to other accounts)
  - Tenant or tenant admin can't another tenant's volume (or a volume's clone, or a snapshot's clone) to their own VAG. Tenant can touch or tamper with another tenant's volumes, snapshots, VAGs
  - Tenant can't roll back their snapshot (also something that may be OK, but I am not very convinced so I haven't implemented this yet)
- Prevention of maladministration
  - No custom QoS values - only pre-created QoS policies
  - No snapshots without expiration datetime - when a snapshot is created, some expiry time must be selected and I picked an arbitrary limit of 100 hours. If you need longer-lasting snapshots, arrange and schedule 
  - No random snapshot names - all user-taken snapshots have timestamp-based names
  - No random clones - tenant can't clone another tenant's volume and mount it (because they only see own volumes and even for those I haven't implemented `CloneVolume` as I've explained above)
- Expose valuable features that SolidFire's own UI did not
  - This is mostly indirectly (in Angular-based SolidFire WAC Extension) and mostly about `attributes`, which SolidFire UI doesn't expose. You can see them in some cases (Volume details, for example), but cannot modify or delete them. In most other cases you can't even see them in the UI
  - The first direct use is hidden Volume attributes which may be extended to snapshots if anyone needs that. For more on this see the FAQs.
  - The second is how SolidFire WAC Gateway uses VAGs (see the FAQs), which isn't recommended but is available. VAG `attributes` are used to limit access to VAGs in the Gateway API, but rely on SolidFire cluster administrators for accurate tagging.
  

The API has granular permissions and can allow power users to perform more actions (still tenant-scoped) such as deleting a QoS policy, for example, but even SolidFire WAC power users can't do anything to volumes that aren't within their scope as defined in `appsettings.json`.

More (including screenshots with SolidFire WAC Extension) can be found in the posts linked in the FAQ section below.


## Accessing SolidFire Gateway API 

See `swagger.json` in this repository or get live Swagger at https://[YOUR_API_GATEWAY:PORT]/swagger/index.html once you deploy.

The gateway is deliberately Spartan and made to disable maladministration while allowing multi-tenancy.

What follows are some examples.


### Get Cluster Info: GET `/SolidFire/{cluster}/clusterinfo`

- Returns information about the specified SolidFire cluster
- Replace `{cluster}` with a cluster identifier (e.g., `PROD`, `DR`) you set in `appsettings.json` (before you rebuilt and redeployed your gateway).


### Access API from PowerShell

As a Windows account authorized to access this endpoint:

```powershell
Invoke-RestMethod -Uri "https://[YOUR_API_GATEWAY:PORT]/SolidFire/[CLUSTER_ID]/clusterinfo" -UseDefaultCredentials
```


### List Volumes for Account: GET `/SolidFire/{cluster}/listvolumesforaccount?accountID={id}`

- Returns all volumes for the specified account ID on the given cluster.
- Replace `{cluster}` with the cluster identifier from `appsettings.json` and  `{id}` with the account ID `TenantOptions.AllowedTenants`.
  - You won't be able to see accounts other than the account ID(s) in that `TenantOptions.AllowedTenants` list, unless your account is (or belongs to a group) in `GlobalAdminRoles` (same settings file).


### Workflow: use SolidFire WAC Gateway to provision storage to Hyper-V cluster

Refer to Swagger if you need more details, but you need 

- Get your account ID, name and either CHAP credentials or a confirmation that admin has created a VAG for you (see the FAQs)
- `GET /SolidFire/{cluster}/clusterinfo` - to get cluster SVIP i.e. SolidFire iSCSI Portal 
- `POST /SolidFire/{cluster}/volumes` - create volumes for your account ID (if they were already created, skip this step)
- `POST /SolidFire/{cluster}/volumes/foraccount` - list volumes for your account ID

Now that you have the SVIP and IQN(s), you can use Windows iSCSI initiator to login to SolidFire iSCSI portal (SVIP), create volumes and configure Hyper-V Cluster Shared volumes. Optional next steps include fiddling with volume QoS settings and attributes. More detailed steps for Windows users [can be found here](https://github.com/scaleoutsean/solidfire-windows) - most of it should be relevant and applicable.


## FAQ

I haven't gotten any questions, let alone frequent ones, but I can think of some likely ones.


### Do I need Windows Admin Center (WAC) to use this Gateway?

No, there are no dependencies. 

It's just that [WAC was *the main reason* I developed this](https://scaleoutsean.github.io/2025/07/26/solidfire-windows-admin-center-gateway.html). I [later realized](https://scaleoutsean.github.io/2025/07/30/solidfire-windows-admin-center-extension.html) that's not for me given how badly maintained WAC is. 

I didn't change anything in the code to somehow "disable" WAC, so you can use this from any client that can manage Windows authentication and use RESTful APIs. I have been thinking to develop a simple dedicated client for SolidFire WAC Gateway, for example.


### Is there a SolidFire WAC Extension?

Not from me (why see the second link in the above FAQ). I may still [post](https://github.com/scaleoutsean/solidfire-wac-extension) my SolidFire WAC Extension code on Github. It's fully functional as stand-alone applicatin, but I gave up on WAC and haven't tested it from *within* WAC (neither packged nor side-loaded). 

You could develop your own WAC Extension and although you can target the SolidFire API rather than my SolidFire WAC Gateway API, I think SolidFire's API is unsuitable for multi-tenancy even in semi-trusted environments, whereas my API is.


### What to use to list volumes for account?

- `GET /SolidFire/{cluster}/listvolumesforaccount?accountID={any}` - get list of volumes of any account ID, accessible to users in`GlobalAdminRoles` 
- `GET /SolidFire/{cluster}/volumes/foraccount?accountID={id}` - for tenant-scoped roles authorized to access this method with `Get`

Global admins, if any, could use the former for everything. Tenant-scoped users authorized to access the second could only use that one.


### How to test volume access control (VolumeAccess.ActionRoles)

The gateway uses the `VolumeAccess.ActionRoles` section in `appsettings.json` to limit Active Directory (AD) users or groups who can perform certain actions (such as viewing volumes for the account).

As a user NOT in an allowed AD group:

```powershell
Invoke-RestMethod -Uri "https://[YOUR_API_GATEWAY:PORT]/SolidFire/[CLUSTER]/clusterinfo" -UseDefaultCredentials
```

You're expected to see something like this:

```pwsh
Invoke-RestMethod : The remote server returned an error: (403) Forbidden.
At line:1 char:1
+ Invoke-RestMethod -Uri "https://win25:5000/SolidFire/DR/listvolumesfo ...
+ ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : InvalidOperation: (System.Net.HttpWebRequest:HttpWebRequest) [Invoke-RestMethod], WebException
    + FullyQualifiedErrorId : WebCmdletWebResponseException,Microsoft.PowerShell.Commands.InvokeRestMethodCommand
```

Or in Postman:

```json
{
  "error": "access_denied",
  "message": "You do not have permission to perform this action."
}
```

When it works, that is obvious.

```pwsh
PS C:\Users\Administrator> Invoke-RestMethod -Uri "https://win25:5000/SolidFire/DR/listvolumesforaccount?accountID=4" -UseDefaultCredentials

id result
-- ------
 1 @{volumes=System.Object[]}

PS C:\Users\Administrator> (Invoke-RestMethod -Uri "https://win25:5000/SolidFire/DR/listvolumesforaccount?accountID=4" -UseDefaultCredentials).result.volumes | ft

access    accountID attributes blockSize createTime           deleteTime enable512e fifoSize iqn
------    --------- ---------- --------- ----------           ---------- ---------- -------- ---
readWrite         4                 4096 2025-07-06T19:09:55Z                 False        5 iqn.2010-01.com.solidfire:xh67.dc1-dr-...
```

![SolidFire WAC Gateway authN vs authZ](/images/solidfire-wac-gateway-authn-authz.png)

Similarly, non-global-scoped users that are authorized to `listvolumesforaccount?accountID=4` should fail to accomplish the same for any other account ID not in their `appsettings.json`.


### What account to use to test

For my purpose I created an AD account that isn't in `appsettings.json` (called `npc`) and used that as well as my "whitelisted" account to run PowerShell commands. To run as `npc`:

```pwsh
runas /user:npc "powershell"
```

You could also remove your own account from `appsettings.json`, rebuild, redeploy, and try all API endpoints using `Invoke-RestMethod`. All API calls or PowerShell commands for those endpoints should fail.

SolidFire WAC Gateway has a `whoami` diagnostic endpoint where you can check your effective identity. My example:

```pwsh
PS C:\inetpub\SolidFireGateway> Invoke-RestMethod -Uri "https://win25:5000/diag/whoami" -UseDefaultCredentials | ConvertTo-Json -Depth 5
{
  "name": "DATAFABRIC\\Administrator",
  "groups": [
    "Everyone",
    "BUILTIN\\Users",
    "BUILTIN\\Certificate Service DCOM Access",
    "BUILTIN\\Pre-Windows 2000 Compatible Access",
    "NT AUTHORITY\\SERVICE",
    "CONSOLE LOGON",
    "NT AUTHORITY\\Authenticated Users",
    "NT AUTHORITY\\This Organization",
    "BUILTIN\\IIS_IUSRS",
    "LOCAL",
    "S-1-5-82-0"
  ]
}
```


### How to implement HA for SolidFire WAC Gateway?

It's stateless, there's no database or write cache. 

Just deploy to multiple IIS servers and make sure:

- they accept connections from all CLI and API clients that need to be able to access them
- both IIS have access to highly-available ADS and DNS services
- both can load credentials for back-end SolidFire and reach it


### Which browsers and clients can be used?

- MS Edge - it should just work and give a prompt 
- Firefox - you may need to edit your browser [settings](https://superuser.com/a/1455054) (`about:config`). I had to on my Firefox
- PowerShell on Windows - add `-UseDefaultCredentials` 
- PowerShell on non-Windows - haven't tested


### Where should SolidFire WAC Gateway be deployed?

As per diagram at the top.

You could deploy it on management cluster so that IIS serves the application only to local clients on management nodes.


### Multi-tenancy of SolidFire WAC Gateway

As I've mentioned above, in version 1 all tenant-scoped resources require explicit control and each gateway is scoped to a list of account IDs that can include one or more tenants/accounts. 

What that means is you can deploy a single SolidFire WAC Gateway that allows access to tenants `[4, 5]`. This may be okay for Engineering Division where one IT team manage two Hyper-V clusters (factory A and factory B, Databases and Web apps etc.), each of whom uses their own account ID. 

If you don't want that, then deploy multiple IIS sites, each with one SolidFire WAC Gateway (pepsi.lan, coke.lan) scoped to a single account ID.

  - Since we're talking about Coke & Pepsi here: each team should have no global-scoped admins in `appsettings.json` to prevent any gateway user even seeing volume details of the other tenant
  - Notice that in this case none of gateway users whatsoever have access to the SolidFire API
  - However, leaked SolidFire credentials *and* access to back-end storage management network would allow anyone to gain direct and unimpeded access to SolidFire API, which is the main thing SolidFire WAC Gateway aims to prevent


### How to use VAGs with SolidFire WAC Gateway?

- You don't have to, if you use CHAP to login to iSCSI targets. But you may, although it's archaic.
- Use an API client or PowerShell to connect directly to SolidFire API (not SolidFire WAC Gateway!) and create a VAG with the `attributes` of `{"accountID": INT}` where `INT` is the account ID (SolidFire tenant) who is supposed to use the VAG from SolidFire WAC Gateway instance.

```json
{
    "method": "CreateVolumeAccessGroup",
    "params": {
        "name": "accountNameHere",
        "attributes": {
          "accountID": 4
        }
    },
    "id": 1
}
```

- SolidFire WAC Gateway instance, when it lists VAGs, exposes only those that match the tenant ID the Gateway is scoped to, e.g. `4`.
- This tenant can add or add their volumes to, or remove them from, this VAG.

### What are hidden volume attributes?

In short: those are volume `attributes` which have the keys that starts with `_reserved`. SolidFire WAC Gateway stripes them before delivering the rest to its front end API users, thereby "hiding" them. Read on for examples and use cases.

SolidFire WAC Gateway allows users to set several (in SolidFire WAC Extension, up to 3) volume attributes with total size (of `attributes` value which is a JSON object) of 256 bytes.

Because in the process they could delete any attributes set by storage admins or "power admins" of SolidFire WAC Gateway, there are two options to prevent them from doing that: 
- Disable write/modify on tags in the UI (like NetApp did) - low energy idea
- Split and hide (what SolidFire WAC Gateway does) 
  - Users can set up to 3 attributes totaling 256 bytes for own use
  - Admins can set some for their own use, and need to prefix their keys with `_reserved` (example below)
  - SolidFire WAC Gateway "hides" `_reserved*` keys on its front end, so its tenants don't see such keys, and this works as long as:
    - Both parties together stay within the limits (10 KV pairs and not more than 1000 bytes for the entire JSON objects)
    - Admins don't take up more than 7 keys (in which case the API would have undefined response) or (1000-(256-2)) bytes for 7 KV pairs

```json
"attributes": {
  "_reserve_replication": true,
  "_reserve_paired_cluster": "prod",
  "_reserve_s3_backup": true
}    
```

Gateway doesn't do very detailed validation on these limits and isn't supposed to do anything about "violations" done by storage admins who bypass Gateway. Secondly, even malicious use can't cause serious problems. 

Thirdly, Gateway restricts front-end use to up to 3 KV pairs and up to 256 bytes (actually 254 bytes after closing `{}`s are deducted). If Gateway API users go over 3 KVs or 256 bytes, Gateway returns `400 BadRequest` with an `attributesTooLarge` error.

There's plenty for volume owner and plenty for storage admins as well. Admins just need to use less than 8 and don't go over 746 on their side, to leave 256 (inclusive of `{}`s) for users. Once you have these in place, users and admins can automate replication, DR, backup and other operations where an API can receive instructions based on these KV pairs.


### Can I use SolidFire WAC to achieve Quantum Computing-resistant encryption?

On the front-end, absolutely! We'd have to:

- modify Gateway for .NET 10 (currently in Preview 6)
- optionally, create a reverse proxy endpoint for native SolidFire API, so that all "native" SolidFire plugins and apps can use native SolidFire 

Note that those apps would have to be rebuilt for .NET 10 to take advantage of PQC. SolidFire PowerShell Tools are probably out of question and SolidFire Plugin for vCenter as well. 

But proxied API access from native PowerShell (.NET 10) RESTful clients would use the same PQC that .NET 10 will provide. I think encryption vendors (SafeNet, for example) will rebuild their clients for .NET 10 quickly after it's released so whether you use generic IIS or Gateway on IIS, you'd probably be better off with .NET 10.


## Security-related notes

SolidFire WAC Gateway hasn't been "audited", "proven" or anything like that, so feel free to inspect the source code and try a few ways - especially from unauthenticated clients or authenticated Windows accounts not in `appsettings.json` to access its API methods and confirm you're getting expected results.

SolidFire credentials in `appsettings.json` are used by SolidFire WAC Gateway (to authenticate against back-end SolidFire cluster(s)), not by gateway's API users. Gateway's API users just need to authenticate against Gateway using Windows authentication on IIS, so no new (or any other) passwords are required or available there. If you want you can add additional authentication to API gateway on your own.


### Restrict access to certain IPs 

Since Windows authentication is the only layer (no 2FA), you may want to limit IP access SWG using IIS.

Start by installing the IIS plugin (`Install-WindowsFeature Web-IP-Security`) and then [configure](https://www.server-world.info/en/note?os=Windows_Server_2025&p=iis&f=11) it to allow access to only management domain IP(s) and deny it to all others.

One can run SWAG IIS web site on your management nodes and close your SWAG ports on external firewall, so that it essentially serves only "loopback" clients. You'd still need a valid TLS certificate.


### IIS and Application logs

Guard your API gateway's IIS logs carefully. If you turn on “failed request tracing” (FREB) or custom IIS logging that explicitly dumps headers or server variables, that could inadvertently capture the Authorization header. As a best practice, have tight ACLs on the logs directory in any case.


### Protecting back-end (SolidFire) credentials

You may remove SolidFire credentials from production code.

- Development

```pwsh
cd c:\Users\Administrator\Projects\SolidFireGateway
dotnet user-secrets init
dotnet user-secrets set "SolidFireClusters:PROD:Username" "admin"
dotnet user-secrets set "SolidFireClusters:PROD:Password" "s3creT"
```

- Production
 
Store SolidFire cluster admin credentials in a vault under the same key names (e.g. in my `appsettings.json` I have `SolidFireClusters__PROD__Username`, `SolidFireClusters__PROD__Password`) and load them on startup.


## Development

- .NET 8.0 SDK
- IIS (I used Windows Server 2025 but any with .NET 8 support should be fine)
- SolidFire 12.x

SolidFire-specific debug logs are available with `"SolidFireGateway.SolidFireClient": "Debug"` in `appsettings.json`. This logs JSON-RPC requests (body only) to files in the gateway server's IIS Web site's log directory. Both `Debug` or `Trace` result RPC logs being generated, while `Information` turns RPC logging off.


### Additional notes

- Get a valid FQDN and TLS certificate for Web/API server, otherwise all your Windows users' credentials may get compromised and/or abused
- Stand-alone Kestrel is a PITA. I wonder if .NET on Linux works as well. IIS works well, and especially if `web.config` is deployed to IIS together with gateway/application
- Until I started pushing the app and `web.config` together, I had big problems with IIS as it wouldn't let me disable Anonymous Authentication on the gateway IIS site. I don't recommend this, but here is what I did to change that globally:
 - Make a backup of, and edit `C:\Windows\System32\inetsrv\config\applicationHost.config`
 - In section `<sectionGroup name="system.webServer">` configure Anonymous and Windows authentication as required and restart IIS with `iisreset`

```xml
<section name="windowsAuthentication" overrideModeDefault="Allow" />
<section name="anonymousAuthentication" overrideModeDefault="Allow" />
```

