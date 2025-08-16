using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SolidFireGateway;
using Microsoft.Extensions.Logging.Abstractions;

// Placeholder HTTP templates for VolumesController:
// GET    /SolidFire/{cluster}/volumes/foraccount?accountID={accountID}
// POST   /SolidFire/{cluster}/volumes
// GET    /SolidFire/{cluster}/volumes/{volumeID}
// DELETE /SolidFire/{cluster}/volumes/{volumeID}
// POST   /SolidFire/{cluster}/volumes/purge

namespace SolidFireGateway.Controllers
{
    [ApiController]
    [Route("SolidFire/{cluster}/volumes")]
    public class VolumesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly VolumeAccessService _accessService;
        private readonly TenantOptions _tenantOptions;
        private readonly Microsoft.Extensions.Logging.ILogger<VolumesController> _logger;

    // Convert attribute values (JsonElement or primitive) to native CLR types
    private object NormalizeAttributeValue(object value)
    {
        if (value is JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.Number:
                    if (je.TryGetInt32(out var i)) return i;
                    if (je.TryGetDouble(out var d)) return d;
                    break;
                case JsonValueKind.String:
                    return je.GetString();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return je.GetBoolean();
                default:
                    return je.ToString();
            }
        }
        if (value is JToken jt)
        {
            switch (jt.Type)
            {
                case JTokenType.Integer: return jt.Value<int>();
                case JTokenType.Float: return jt.Value<double>();
                case JTokenType.String: return jt.Value<string>();
                case JTokenType.Boolean: return jt.Value<bool>();
                default: return jt.ToString();
            }
        }
        return value;
    }

        public VolumesController(IHttpClientFactory httpClientFactory, IConfiguration config, IOptions<TenantOptions> tenantOptions, Microsoft.Extensions.Logging.ILogger<VolumesController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _accessService = new VolumeAccessService(config);
            _tenantOptions = tenantOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// Lists volumes for a given account.
        /// </summary>
        /// <remarks>
        /// Enforces VolumeAccess:List role-based access and tenant scoping.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        /// <param name="accountID">Tenant account ID to scope volumes.</param>
        [HttpGet("foraccount")]
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
                if (!_accessService.IsUserInRole(user, groupNames, "List"))
                {
                    return Forbid();
                }

                // Tenant scoping: only allow configured tenants
                if (!_tenantOptions.AllowedTenants.Contains(accountID))
                {
                    return Forbid();
                }

                var httpClient = _httpClientFactory.CreateClient(cluster);
                var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);

                var parameters = new ListVolumesForAccountParams
                {
                    accountID = accountID,
                    includeVirtualVolumes = false
                };

                var fullResult = await client.SendRequestAsync<ListVolumesForAccountResult>("ListVolumesForAccount", parameters);

                if (fullResult.volumes == null || fullResult.volumes.Count == 0)
                {
                    return NotFound(new { id = 1, cluster = cluster, error = "notfound", message = "No volumes found for account" });
                }

                // Trim volumes for response
                var trimmedVolumes = fullResult.volumes.Select(vol => new {
                    access = vol.access,
                    accountID = vol.accountID,
                    attributes = vol.attributes != null
                        ? vol.attributes.ToDictionary(
                            kvp => kvp.Key,
                            kvp => NormalizeAttributeValue(kvp.Value))
                        : null,
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

                return new JsonResult(new {
                    id = 1,
                    result = new { volumes = trimmedVolumes }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new { id = 1, cluster = cluster, error = "unreachable", message = ex.Message });
            }
        }

        /// <summary>
        /// Creates a new volume in the specified account.
        /// </summary>
        /// <remarks>
        /// Enforces VolumeAccess:Create role-based access and tenant scoping.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        /// <param name="createParams">Parameters for creating a new volume.</param>
        [HttpPost]
        public async Task<ActionResult<CreateVolumeResponse>> CreateVolume(string cluster, [FromBody] CreateVolumeParams createParams)
        {
            try
            {
                // Log user and groups for debugging
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
                var allowedRoles = _accessService.GetActionRoles("Create");
                _logger.LogInformation("CreateVolume attempt by user {User} with groups {Groups}. Allowed roles: {AllowedRoles}", user, groupNames, allowedRoles);
                // Access control: Only allow users in ActionRoles:Create
                if (!_accessService.IsUserInRole(user, groupNames, "Create"))
                {
                    _logger.LogWarning("User {User} not authorized to CreateVolume", user);
                    return Forbid();
                }

                // Tenant scoping: only allow configured tenants
                if (!_tenantOptions.AllowedTenants.Contains(createParams.accountID))
                {
                    return Forbid();
                }
                // Enforce maximum JSON byte size on attributes (<=256 bytes)
                if (createParams.attributes != null && createParams.attributes.Count > 0)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(createParams.attributes);
                    var size = System.Text.Encoding.UTF8.GetByteCount(json);
                    if (size > 256)
                        return BadRequest(new { error = "attributesTooLarge", message = $"Attributes payload is {size} bytes; max is 256." });
                }

                var httpClient = _httpClientFactory.CreateClient(cluster);
                var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);

                var result = await client.SendRequestAsync<CreateVolumeResult>("CreateVolume", createParams);

                var response = new CreateVolumeResponse
                {
                    id = 1,
                    result = result
                };

                return new JsonResult(response);
            }
            catch (Exception ex)
            {
                return StatusCode(503, new { id = 1, cluster = cluster, error = "unreachable", message = ex.Message });
            }
        }

        /// <summary>
        /// Modifies properties of an existing volume.
        /// </summary>
        /// <remarks>
        /// Enforces VolumeAccess:Update role-based access and ensures volume ownership across allowed tenants.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        /// <param name="modifyParams">Parameters for modifying an existing volume.</param>
        [HttpPut]
        public async Task<ActionResult<ModifyVolumeResponse>> ModifyVolume(string cluster, [FromBody] ModifyVolumeParams modifyParams)
        {
            try
            {
                // Access control: Only allow users in ActionRoles:Update
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
                if (!_accessService.IsUserInRole(user, groupNames, "Update"))
                    return Forbid();

                // Tenant scoping: ensure the volume belongs to a configured tenant by listing volumes per allowed tenant
                var httpClient = _httpClientFactory.CreateClient(cluster);
                var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);
                bool authorized = false;
                foreach (var accId in _tenantOptions.AllowedTenants)
                {
                    var listResult = await client.SendRequestAsync<ListVolumesForAccountResult>(
                        "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false });
                    if (listResult?.volumes != null && listResult.volumes.Any(v => v.volumeID == modifyParams.volumeID))
                    {
                        authorized = true;
                        break;
                    }
                }
                if (!authorized)
                {
                    return Forbid();
                }
                // Enforce maximum JSON byte size on attributes (<=256 bytes)
                if (modifyParams.attributes != null && modifyParams.attributes.Count > 0)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(modifyParams.attributes);
                    var size = System.Text.Encoding.UTF8.GetByteCount(json);
                    if (size > 256)
                        return BadRequest(new { error = "attributesTooLarge", message = $"Attributes payload is {size} bytes; max is 256." });
                }

                // Fetch existing volume attributes
                var existingAttrs = new Dictionary<string, object>();
                foreach (var accId in _tenantOptions.AllowedTenants)
                {
                    var listResult = await client.SendRequestAsync<ListVolumesForAccountResult>(
                        "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false });
                    var vol = listResult.volumes?.FirstOrDefault(v => v.volumeID == modifyParams.volumeID);
                    if (vol != null)
                    {
                        existingAttrs = vol.attributes ?? new Dictionary<string, object>();
                        break;
                    }
                }
                // Normalize existing attribute values (JsonElement to CLR types)
                existingAttrs = existingAttrs
                    .ToDictionary(kvp => kvp.Key, kvp => NormalizeAttributeValue(kvp.Value));
                // Separate reserved and user attributes
                var reservedAttrs = existingAttrs
                    .Where(kvp => kvp.Key.StartsWith("_reserved"))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                var userAttrs = existingAttrs
                    .Where(kvp => !kvp.Key.StartsWith("_reserved"))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                // Validate new user attributes limit
                var newUserAttrs = modifyParams.attributes ?? new Dictionary<string, object>();
                if (newUserAttrs.Count > 3)
                    return BadRequest(new { id = 1, error = "tooManyAttributes", message = "Maximum 3 user attributes allowed" });
                // Merge reserved and new user attributes
                var mergedAttrs = new Dictionary<string, object>(reservedAttrs);
                foreach (var kv in newUserAttrs)
                    mergedAttrs[kv.Key] = kv.Value;
                modifyParams.attributes = mergedAttrs;

                var result = await client.SendRequestAsync<ModifyVolumeResult>("ModifyVolume", modifyParams);
                var response = new ModifyVolumeResponse { id = 1, result = result };
                return new JsonResult(response);
            }
            catch (Exception ex)
            {
                return StatusCode(503, new { id = 1, cluster = cluster, error = "unreachable", message = ex.Message });
            }
        }

        /// <summary>
        /// Deletes an existing volume.
        /// </summary>
        /// <remarks>
        /// Enforces VolumeAccess:Delete role-based access and tenant ownership via volume lookup.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        /// <param name="volumeID">ID of the volume to delete (from route).</param>
        [HttpDelete("{volumeID:int}")]
        public async Task<ActionResult<object>> DeleteVolume(string cluster, int volumeID)
        {
            try
            {
                // Access control: Only allow users in ActionRoles:Delete
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
                if (!_accessService.IsUserInRole(user, groupNames, "Delete"))
                    return Forbid();

                // Tenant scoping: ensure the volume belongs to a configured tenant by listing volumes per allowed tenant
                var httpClient = _httpClientFactory.CreateClient(cluster);
                var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);
                bool authorized = false;
                foreach (var accId in _tenantOptions.AllowedTenants)
                {
                    var listResult = await client.SendRequestAsync<ListVolumesForAccountResult>(
                        "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false });
                    if (listResult?.volumes != null && listResult.volumes.Any(v => v.volumeID == volumeID))
                    {
                        authorized = true;
                        break;
                    }
                }
                if (!authorized)
                {
                    return Forbid();
                }

                var result = await client.SendRequestAsync<DeleteVolumeResult>("DeleteVolume", new DeleteVolumeParams { volumeID = volumeID });

                // Trimmed response
                var vol = result.volume;
                return new JsonResult(new {
                    id = 1,
                    result = new {
                        volumeID = vol.volumeID,
                        deleteTime = vol.deleteTime,
                        purgeTime = vol.purgeTime,
                        iqn = vol.iqn,
                        status = vol.status,
                        volumeAccessGroups = vol.volumeAccessGroups,
                        volumePairs = vol.volumePairs,
                        attributes = vol.attributes
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new { id = 1, cluster = cluster, error = "unreachable", message = ex.Message });
            }
        }

        /// <summary>
        /// Purges a volume that has been previously deleted.
        /// </summary>
        /// <remarks>
        /// Enforces VolumeAccess:Purge role-based access and tenant ownership.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        /// <param name="p">Parameters for purging a deleted volume (i.e. volumeID as this method maps to SolidFire's PurgeDeletedVolume).</param>
        [HttpPost("purge")]
        public async Task<ActionResult<object>> PurgeVolume(string cluster, [FromBody] PurgeVolumeParams p)
        {
            try
            {
                // Access control: Only allow users in ActionRoles:Purge
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
                if (!_accessService.IsUserInRole(user, groupNames, "Purge"))
                    return Forbid();

                // Tenant scoping: ensure the volume belongs to a configured tenant by listing volumes per allowed tenant
                var httpClient = _httpClientFactory.CreateClient(cluster);
                var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);
                bool authorized = false;
                foreach (var accId in _tenantOptions.AllowedTenants)
                {
                    var listResult = await client.SendRequestAsync<ListVolumesForAccountResult>(
                        "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false });
                    if (listResult?.volumes != null && listResult.volumes.Any(v => v.volumeID == p.volumeID))
                    {
                        authorized = true;
                        break;
                    }
                }
                if (!authorized)
                {
                    return Forbid();
                }

                // ...client already initialized above

                // Call PurgeDeletedVolume; result is empty
                await client.SendRequestAsync<object>("PurgeDeletedVolume", p);

                var response = new
                {
                    id = 1
                };

                return new JsonResult(response);
            }
            catch (Exception ex)
            {
                return StatusCode(503, new { id = 1, cluster = cluster, error = "unreachable", message = ex.Message });
            }
        }
    }

    // DTOs for Delete and Purge operations
    public class DeleteVolumeParams
    {
        public int volumeID { get; set; }
    }
    public class PurgeVolumeParams
    {
        public int volumeID { get; set; }
    }
}
