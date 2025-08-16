using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SolidFireGateway; // For ListVolumesForAccountResponse types

// Placeholder HTTP templates for SolidFireController:
// GET    /SolidFire/{cluster}/clusterinfo
// GET    /SolidFire/{cluster}/listvolumesforaccount?accountID={accountID}

namespace SolidFireGateway
{
    [ApiController]
    [Route("SolidFire/{cluster}")]
    public class SolidFireController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly VolumeAccessService _accessService;
        private readonly TenantOptions _tenantOptions;
        private readonly string[] _globalAdmins;

        public SolidFireController(IHttpClientFactory httpClientFactory, IConfiguration config, IOptions<TenantOptions> tenantOptions)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _tenantOptions = tenantOptions.Value;
            _accessService = new VolumeAccessService(config);
            _globalAdmins = config.GetSection("GlobalAdminRoles").Get<string[]>() ?? Array.Empty<string>();
        }

        /// <summary>
        /// Retrieves high-level information about the specified SolidFire cluster.
        /// </summary>
        /// <remarks>
        /// Accessible to any authenticated user. No tenant or role checks are applied.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        [HttpGet("clusterinfo")]
        public async Task<ActionResult<ClusterInfoResponse>> GetClusterInfo(string cluster)
        {
            try
            {
                // No role-based guard for cluster info; allow any authenticated user

                // Get the named client for the requested cluster
                var httpClient = _httpClientFactory.CreateClient(cluster);
                var client = new SolidFireClient(httpClient, _config);

                // Call the JSON-RPC method
                var fullResult = await client.SendRequestAsync<ClusterInfoResult>("GetClusterInfo", new { });

                // Debug: log the result
                System.Console.WriteLine($"[DEBUG] Cluster: {cluster}, Result: {System.Text.Json.JsonSerializer.Serialize(fullResult)}");

                // Build the response in the format you want
                var response = new ClusterInfoResponse
                {
                    id = 1,
                    cluster = cluster,
                    result = fullResult
                };

                // Always return JSON
                return new JsonResult(response);
            }
            catch (System.Exception ex)
            {
                // Log the error
                System.Console.WriteLine($"[ERROR] Cluster: {cluster}, Exception: {ex.Message}");
                // Return a 503 with a friendly error for the frontend
                return StatusCode(503, new { id = 1, cluster = cluster, error = "unreachable", message = ex.Message });
            }
        }

        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        /// <param name="accountID">Tenant account ID to list volumes for.</param>
        /// <summary>
        /// Lists all volumes for a specified account ID on the given cluster.
        /// </summary>
        /// <remarks>
        /// Accessible only to users in GlobalAdminRoles. Allows global admins to list volumes for any SolidFire tenant account.
        /// </remarks>
        [HttpGet("listvolumesforaccount")]
        public async Task<ActionResult<object>> ListVolumesForAccount(string cluster, [FromQuery] int accountID)
        {
            try
            {
                // Access control: Only allow users in ActionRoles:List
                var user = User.Identity?.Name ?? string.Empty;
                var userGroups = User.Claims
                    .Where(c => c.Type == System.Security.Claims.ClaimTypes.GroupSid)
                    .Select(c => c.Value)
                    .ToList();
                var groupNames = new List<string>();
                foreach (var sid in userGroups)
                {
                    try { groupNames.Add(new System.Security.Principal.SecurityIdentifier(sid).Translate(typeof(System.Security.Principal.NTAccount)).ToString()); } catch { }
                }
                // Debug output: print user and group names
                System.Console.WriteLine($"[DEBUG] User: {user}");
                System.Console.WriteLine("[DEBUG] User group names:");
                foreach (var g in groupNames) System.Console.WriteLine($"  - {g}");
                if (!_accessService.IsUserInRole(user, groupNames, "List"))
                {
                    return Forbid();
                }
                // Tenant scoping: allow global admins to bypass
                if (!_globalAdmins.Any(r => User.IsInRole(r)) && !_tenantOptions.AllowedTenants.Contains(accountID))
                {
                    System.Console.WriteLine($"[DEBUG] Access denied: account {accountID} not in allowed tenants");
                    return Forbid();
                }
                var httpClient = _httpClientFactory.CreateClient(cluster);
                var client = new SolidFireClient(httpClient, _config);

                var parameters = new ListVolumesForAccountParams
                {
                    accountID = accountID,
                    includeVirtualVolumes = false // Always false for Windows/Hyper-V,, this VMware-specific
                };

                var fullResult = await client.SendRequestAsync<ListVolumesForAccountResult>("ListVolumesForAccount", parameters);

                // Debug: log the result
                System.Console.WriteLine($"[DEBUG] Cluster: {cluster}, ListVolumesForAccount: {System.Text.Json.JsonSerializer.Serialize(fullResult)}");

                if (fullResult.volumes == null || fullResult.volumes.Count == 0)
                {
                    return NotFound(new { id = 1, cluster = cluster, error = "notfound", message = "No volumes found for account" });
                }

                // Build an array of trimmed volumes
                var trimmedVolumes = fullResult.volumes.Select(vol => new {
                    access = vol.access,
                    accountID = vol.accountID,
                    attributes = vol.attributes,
                    blockSize = vol.blockSize,
                    createTime = vol.createTime,
                    deleteTime = vol.deleteTime,
                    enable512e = vol.enable512e,
                    fifoSize = vol.fifoSize,
                    iqn = vol.iqn,
                    lastAccessTime = vol.lastAccessTime,
                    lastAccessTimeIO = vol.lastAccessTimeIO,
                    minFifoSize = vol.minFifoSize,
                    name = vol.name,
                    purgeTime = vol.purgeTime,
                    qos = vol.qos == null ? null : new {
                        burstIOPS = vol.qos.burstIOPS,
                        maxIOPS = vol.qos.maxIOPS,
                        minIOPS = vol.qos.minIOPS
                    },
                    qosPolicyID = vol.qosPolicyID,
                    scsiEUIDeviceID = vol.scsiEUIDeviceID,
                    scsiNAADeviceID = vol.scsiNAADeviceID,
                    status = vol.status,
                    totalSize = vol.totalSize,
                    volumeAccessGroups = vol.volumeAccessGroups,
                    volumeConsistencyGroupUUID = vol.volumeConsistencyGroupUUID,
                    volumeID = vol.volumeID,
                    volumePairs = vol.volumePairs,
                    volumeUUID = vol.volumeUUID
                }).ToList();

                // Debug: log the trimmed volumes array
                System.Console.WriteLine($"[DEBUG] Cluster: {cluster}, Trimmed volumes: {System.Text.Json.JsonSerializer.Serialize(trimmedVolumes)}");

                return new JsonResult(new {
                    id = 1,
                    result = new {
                        volumes = trimmedVolumes
                    }
                });
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[ERROR] Cluster: {cluster}, ListVolumesForAccount Exception: {ex.Message}");
                return StatusCode(503, new { id = 1, cluster = cluster, error = "unreachable", message = ex.Message });
            }
        }
    }
}
