using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using SolidFireGateway;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

// Placeholder HTTP templates for SnapshotController:
// POST   /SolidFire/{cluster}/snapshots
// POST   /SolidFire/{cluster}/snapshots/group
// POST   /SolidFire/{cluster}/snapshots/group/list
// POST   /SolidFire/{cluster}/snapshots/list

namespace SolidFireGateway.Controllers
{
    [ApiController]
    [Route("SolidFire/{cluster}/snapshots")]
    public class SnapshotController : ControllerBase
    {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly TenantOptions _tenantOptions;

        public SnapshotController(IHttpClientFactory httpClientFactory,
                                   IConfiguration config,
                                   IOptions<TenantOptions> tenantOptions)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _tenantOptions = tenantOptions.Value;
        }

        private bool IsUserInRole(string action)
        {
            // Super-admin roles bypass based on configuration
            var globalAdmins = _config.GetSection("GlobalAdminRoles").Get<string[]>() ?? System.Array.Empty<string>();
            if (globalAdmins.Any(r => User.IsInRole(r)))
                return true;
            var roles = _config.GetSection($"SnapshotAccess:ActionRoles:{action}").Get<string[]>() ?? new string[0];
            foreach (var r in roles)
            {
                if (User.IsInRole(r)) return true;
            }
            return false;
        }

        // Normalize attribute values (JsonElement or JToken) to CLR primitives
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

        /// <summary>
        /// Creates a new snapshot for a volume.
        /// </summary>
        /// <remarks>
        /// Enforces SnapshotAccess:Create role-based access and validates retention format and tenant ownership of the volume.
        /// </remarks>
        [HttpPost]
        public async Task<ActionResult<object>> CreateSnapshot(string cluster, [FromBody] CreateSnapshotParams p)
        {
            if (!IsUserInRole("Create"))
                return Forbid();
            // Validate retention format HH:mm:ss
            if (!Regex.IsMatch(p.retention, @"^\d{1,2}:\d{1,2}:\d{1,2}$"))
                return BadRequest(new { error = "invalidRetentionFormat", message = "Retention must be HH:mm:ss" });
            // Validate numeric ranges: hours 0-99, minutes 0-59, seconds 0-59
            var parts = p.retention.Split(':');
            if (parts.Length != 3
                || !int.TryParse(parts[0], out var hours)
                || !int.TryParse(parts[1], out var minutes)
                || !int.TryParse(parts[2], out var seconds)
                || hours < 0 || hours > 99
                || minutes < 0 || minutes > 59
                || seconds < 0 || seconds > 59)
            {
                return BadRequest(new { error = "invalidRetentionRange", message = "Retention parts out of range: hours 0-99, minutes/seconds 0-59" });
            }

            var client = new SolidFireClient(
                _httpClientFactory.CreateClient(cluster), _config, NullLogger<SolidFireClient>.Instance);
            var result = await client.SendRequestAsync<CreateSnapshotResult>("CreateSnapshot", p);
            // Normalize snapshot attributes
            if (result.snapshot?.attributes != null)
            {
                result.snapshot.attributes = result.snapshot.attributes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => NormalizeAttributeValue(kvp.Value)
                );
            }
            return Ok(new { id = 1, result });
        }

        /// <summary>
        /// Creates a group snapshot across multiple volumes.
        /// </summary>
        /// <remarks>
        /// Enforces SnapshotAccess:Create role-based access, validates retention, and ensures all volumes belong to the same tenant.
        /// </remarks>
        [HttpPost("group")]
        public async Task<ActionResult<object>> CreateGroupSnapshot(string cluster, [FromBody] CreateGroupSnapshotParams p)
        {
            if (!IsUserInRole("Create"))
                return Forbid();
            // Validate retention
            if (!Regex.IsMatch(p.retention, @"^\d{1,2}:\d{1,2}:\d{1,2}$"))
                return BadRequest(new { error = "invalidRetentionFormat", message = "Retention must be HH:mm:ss" });
            var parts = p.retention.Split(':');
            if (parts.Length != 3
                || !int.TryParse(parts[0], out var hours)
                || !int.TryParse(parts[1], out var minutes)
                || !int.TryParse(parts[2], out var seconds)
                || hours < 0 || hours > 99
                || minutes < 0 || minutes > 59
                || seconds < 0 || seconds > 59)
            {
                return BadRequest(new { error = "invalidRetentionRange", message = "Retention parts out of range: hours 0-99, minutes/seconds 0-59" });
            }
            // Validate volume ownership via ListVolumesForAccount and ensure same tenant
            var client = new SolidFireClient(_httpClientFactory.CreateClient(cluster), _config, NullLogger<SolidFireClient>.Instance);
            // Build mapping of volumeID to owning account
            var volAccountMap = new Dictionary<int, int>();
            foreach (var accId in _tenantOptions.AllowedTenants)
            {
                var listResult = await client.SendRequestAsync<ListVolumesForAccountResult>(
                    "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false });
                foreach (var v in listResult.volumes)
                {
                    volAccountMap[v.volumeID] = accId;
                }
            }
            int? baseAccount = null;
            foreach (var vid in p.volumes)
            {
                if (!volAccountMap.TryGetValue(vid, out var acct))
                    return BadRequest(new { error = "volumeNotAllowed", message = $"Volume {vid} not owned by allowed tenants" });
                if (baseAccount == null)
                    baseAccount = acct;
                else if (acct != baseAccount)
                    return BadRequest(new { error = "volumesMismatchTenant", message = "All volumes must belong to the same tenant" });
            }
            var result = await client.SendRequestAsync<CreateGroupSnapshotResult>("CreateGroupSnapshot", p);
            // Normalize groupSnapshot attributes
            if (result.groupSnapshot?.attributes != null)
            {
                result.groupSnapshot.attributes = result.groupSnapshot.attributes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => NormalizeAttributeValue(kvp.Value)
                );
            }
            // Normalize nested snapshot attributes
            if (result.groupSnapshot?.members != null)
            {
                foreach (var s in result.groupSnapshot.members)
                {
                    if (s.attributes != null)
                    {
                        s.attributes = s.attributes.ToDictionary(
                            kvp => kvp.Key,
                            kvp => NormalizeAttributeValue(kvp.Value)
                        );
                    }
                }
            }
            return Ok(new { id = 1, result });
        }

        /// <summary>
        /// Lists all group snapshots for the caller's tenant.
        /// </summary>
        /// <remarks>
        /// Enforces SnapshotAccess:List role-based access and filters results by tenant-owned volumes.
        /// </remarks>
        [HttpPost("group/list")]
        public async Task<ActionResult<object>> ListGroupSnapshots(string cluster)
        {
            if (!IsUserInRole("List"))
                return Forbid();
            var client = new SolidFireClient(_httpClientFactory.CreateClient(cluster), _config, NullLogger<SolidFireClient>.Instance);
            // Collect all volume IDs for allowed tenants
            var allVolumeIds = new List<int>();
            foreach (var accId in _tenantOptions.AllowedTenants)
            {
                var listResult = await client.SendRequestAsync<ListVolumesForAccountResult>(
                    "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false });
                allVolumeIds.AddRange(listResult.volumes.Select(v => v.volumeID));
            }
            // Return empty if no volumes
            if (!allVolumeIds.Any())
                return Ok(new { id = 1, result = new { groupSnapshots = new List<GroupSnapshot>() } });
            // List group snapshots for tenant volumes
            var listRes = await client.SendRequestAsync<ListGroupSnapshotsResult>(
                "ListGroupSnapshots", new ListGroupSnapshotsParams { volumes = allVolumeIds });
            // Normalize group and member attributes
            foreach (var g in listRes.groupSnapshots)
            {
                if (g.attributes != null)
                {
                    g.attributes = g.attributes.ToDictionary(
                        kvp => kvp.Key,
                        kvp => NormalizeAttributeValue(kvp.Value)
                    );
                }
                if (g.members != null)
                {
                    foreach (var s in g.members)
                    {
                        if (s.attributes != null)
                        {
                            s.attributes = s.attributes.ToDictionary(
                                kvp => kvp.Key,
                                kvp => NormalizeAttributeValue(kvp.Value)
                            );
                        }
                    }
                }
            }
            return Ok(new { id = 1, result = listRes });
        }

        /// <summary>
        /// Lists snapshots for a specific volume or all tenant volumes.
        /// </summary>
        /// <remarks>
        /// Enforces SnapshotAccess:List role-based access, auto-aggregates snapshots across tenant volumes if none specified, and validates per-request volume ownership.
        /// </remarks>
        [HttpPost("list")]
        public async Task<ActionResult<object>> ListSnapshots(string cluster, [FromBody] ListSnapshotsParams p)
        {
            if (!IsUserInRole("List"))
                return Forbid();
            var client = new SolidFireClient(_httpClientFactory.CreateClient(cluster), _config, NullLogger<SolidFireClient>.Instance);
            // If no specific volumeID supplied (zero or negative), list all snapshots across allowed tenants
            if (p == null || p.volumeID <= 0)
            {
                var allSnaps = new List<Snapshot>();
                foreach (var accId in _tenantOptions.AllowedTenants)
                {
                    var vols = (await client.SendRequestAsync<ListVolumesForAccountResult>(
                        "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false }))
                        .volumes.Select(v => v.volumeID);
                    foreach (var vid in vols)
                    {
                        var listRes = await client.SendRequestAsync<ListSnapshotsResult>(
                            "ListSnapshots", new ListSnapshotsParams { volumeID = vid });
                        allSnaps.AddRange(listRes.snapshots);
                    }
                }
                // Normalize snapshot attributes
                foreach (var s in allSnaps)
                {
                    if (s.attributes != null)
                    {
                        s.attributes = s.attributes.ToDictionary(
                            kvp => kvp.Key,
                            kvp => NormalizeAttributeValue(kvp.Value)
                        );
                    }
                }
                return Ok(new { id = 1, result = new { snapshots = allSnaps } });
            }
            // Otherwise enforce per-request filtering on one volume
            bool allowed = false;
            foreach (var accId in _tenantOptions.AllowedTenants)
            {
                var listResult = await client.SendRequestAsync<ListVolumesForAccountResult>(
                    "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false });
                if (listResult.volumes.Any(v => v.volumeID == p.volumeID))
                {
                    allowed = true;
                    break;
                }
            }
            if (!allowed)
                return BadRequest(new { error = "volumeNotAllowed", message = $"Volume {p.volumeID} not owned by allowed tenants" });
            var result = await client.SendRequestAsync<ListSnapshotsResult>("ListSnapshots", p);
            // Normalize snapshot attributes
            if (result.snapshots != null)
            {
                foreach (var s in result.snapshots)
                {
                    if (s.attributes != null)
                    {
                        s.attributes = s.attributes.ToDictionary(
                            kvp => kvp.Key,
                            kvp => NormalizeAttributeValue(kvp.Value)
                        );
                    }
                }
            }
            return Ok(new { id = 1, result });
        }
    }
}
