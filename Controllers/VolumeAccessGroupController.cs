using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace SolidFireGateway.Controllers
{
    // Placeholder HTTP templates for VolumeAccessGroupController:
    // GET    /SolidFire/{cluster}/volumeaccessgroups
    // POST   /SolidFire/{cluster}/volumeaccessgroups/{volumeAccessGroupID}/volumes
    // DELETE /SolidFire/{cluster}/volumeaccessgroups/{volumeAccessGroupID}/volumes
    [ApiController]
    [Route("SolidFire/{cluster}/volumeaccessgroups")]
    public class VolumeAccessGroupController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly List<int> _allowedTenants;

        public VolumeAccessGroupController(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            IOptions<TenantOptions> tenantOptions)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _allowedTenants = tenantOptions.Value.AllowedTenants;
        }

        // Shared role check using VolumeAccess section
        private bool IsUserAllowed(string action)
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
                return false;
            // super-admin bypass
            var globalAdmins = _config.GetSection("GlobalAdminRoles").Get<string[]>() ?? Array.Empty<string>();
            if (globalAdmins.Any(r => User.IsInRole(r)))
                return true;
            var roles = _config.GetSection($"VolumeAccess:ActionRoles:{action}").Get<string[]>() ?? Array.Empty<string>();
            foreach (var r in roles)
            {
                if (User.IsInRole(r)) return true;
            }
            return false;
        }
        
        // Helper to parse attribute values (JsonElement, string, number) into int
        private bool TryGetAttributeInt(object value, out int result)
        {
            if (value is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out result))
                    return true;
                if (je.ValueKind == JsonValueKind.String && int.TryParse(je.GetString(), out result))
                    return true;
            }
            try
            {
                result = Convert.ToInt32(value);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

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
            return value;
        }

        /// <summary>
        /// Lists all volume access groups available to the caller.
        /// </summary>
        /// <remarks>
        /// Returns only those groups where attributes["accountID"] matches an allowed tenant.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        [HttpGet]
        public async Task<ActionResult<object>> ListVolumeAccessGroups(string cluster)
        {
            if (!IsUserAllowed("List"))
                return Forbid();

            try
            {
                var httpClient = _httpClientFactory.CreateClient(cluster);
                var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);

                // aggregate allowed volumes
                var allowedVolumes = new HashSet<int>();
                foreach (var accId in _allowedTenants)
                {
                    var lv = await client.SendRequestAsync<ListVolumesForAccountResult>(
                        "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false });
                    foreach (var v in lv.volumes)
                        allowedVolumes.Add(v.volumeID);
                }

                // fetch all groups
                var allGroups = await client.SendRequestAsync<ListVolumeAccessGroupsResult>(
                    "ListVolumeAccessGroups", new { });

                // filter by attribute-based ownership only: show groups where attributes["accountID"] matches an allowed tenant
                var filtered = allGroups.volumeAccessGroups
                    .Where(g =>
                        g.attributes != null
                        && g.attributes.TryGetValue("accountID", out var attr)
                        && TryGetAttributeInt(attr, out var ownerId)
                        && _allowedTenants.Contains(ownerId)
                    )
                    .ToList();

                // Normalize JsonElement attribute values to native types
                foreach (var g in filtered)
                {
                    if (g.attributes != null)
                    {
                        g.attributes = g.attributes.ToDictionary(
                            kvp => kvp.Key,
                            kvp => NormalizeAttributeValue(kvp.Value)
                        );
                    }
                }
                return Ok(new { id = 1, result = new { volumeAccessGroups = filtered } });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(503, new { id = 1, cluster = cluster, error = "unreachable", message = ex.Message });
            }
        }

        /// <summary>
        /// Adds volumes to a volume access group.
        /// </summary>
        /// <remarks>
        /// Verifies ownership then adds volumes.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        /// <param name="volumeAccessGroupID">ID of the volume access group to modify.</param>
        /// <param name="p">Add volumes parameters.</param>
        [HttpPost("{volumeAccessGroupID:int}/volumes")]
        public async Task<ActionResult<object>> AddVolumesToVolumeAccessGroup(string cluster, int volumeAccessGroupID, [FromBody] AddVolumesToVolumeAccessGroupParams p)
        {
            if (!IsUserAllowed("Update"))
                return Forbid();

            var httpClient = _httpClientFactory.CreateClient(cluster);
            var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);

            // build allowed volumes set
            var allowedVolumes = new HashSet<int>();
            foreach (var accId in _allowedTenants)
            {
                var lv = await client.SendRequestAsync<ListVolumesForAccountResult>(
                    "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false });
                foreach (var v in lv.volumes)
                    allowedVolumes.Add(v.volumeID);
            }

            // fetch existing group
            var allGroups = await client.SendRequestAsync<ListVolumeAccessGroupsResult>("ListVolumeAccessGroups", new { });
            var group = allGroups.volumeAccessGroups.FirstOrDefault(g => g.volumeAccessGroupID == volumeAccessGroupID);
            if (group == null)
                return NotFound();

            // verify attribute-based ownership first
            if (group.attributes != null && group.attributes.TryGetValue("accountID", out var attrVal))
            {
                if (!TryGetAttributeInt(attrVal, out var owner) || !_allowedTenants.Contains(owner))
                    return BadRequest(new { error = "forbiddenGroup", message = "Group owned by another tenant" });
            }
            else if (group.volumes != null && !group.volumes.All(id => allowedVolumes.Contains(id)))
                return BadRequest(new { error = "forbiddenGroup", message = "Group contains volumes outside your tenants" });

            // verify new volumes ownership
            if (p.volumes == null || !p.volumes.All(id => allowedVolumes.Contains(id)))
                return BadRequest(new { error = "volumeNotAllowed", message = "One or more volumes not owned" });

            // perform add
            var result = await client.SendRequestAsync<AddVolumesToVolumeAccessGroupResult>(
                "AddVolumesToVolumeAccessGroup", new { volumeAccessGroupID, volumes = p.volumes });

            // Normalize attributes before returning
            if (result.volumeAccessGroup.attributes != null)
            {
                result.volumeAccessGroup.attributes = result.volumeAccessGroup.attributes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => NormalizeAttributeValue(kvp.Value)
                );
            }
            return Ok(new { id = 1, result = new { volumeAccessGroup = result.volumeAccessGroup } });
        }

        /// <summary>
        /// Removes volumes from a volume access group.
        /// </summary>
        /// <remarks>
        /// Verifies ownership then removes volumes.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        /// <param name="volumeAccessGroupID">ID of the volume access group to modify.</param>
        /// <param name="p">Remove volumes parameters.</param>
        [HttpDelete("{volumeAccessGroupID:int}/volumes")]
        public async Task<ActionResult<object>> RemoveVolumesFromVolumeAccessGroup(string cluster, int volumeAccessGroupID, [FromBody] RemoveVolumesFromVolumeAccessGroupParams p)
        {
            if (!IsUserAllowed("Update"))
                return Forbid();

            var httpClient = _httpClientFactory.CreateClient(cluster);
            var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);

            // build allowed volumes set
            var allowedVolumes = new HashSet<int>();
            foreach (var accId in _allowedTenants)
            {
                var lv = await client.SendRequestAsync<ListVolumesForAccountResult>(
                    "ListVolumesForAccount", new ListVolumesForAccountParams { accountID = accId, includeVirtualVolumes = false });
                foreach (var v in lv.volumes)
                    allowedVolumes.Add(v.volumeID);
            }

            // fetch existing group
            var allGroups = await client.SendRequestAsync<ListVolumeAccessGroupsResult>("ListVolumeAccessGroups", new { });
            var group = allGroups.volumeAccessGroups.FirstOrDefault(g => g.volumeAccessGroupID == volumeAccessGroupID);
            if (group == null)
                return NotFound();

            // verify attribute-based ownership first
            if (group.attributes != null && group.attributes.TryGetValue("accountID", out var ownerAttr))
            {
                if (!TryGetAttributeInt(ownerAttr, out var owner) || !_allowedTenants.Contains(owner))
                    return BadRequest(new { error = "forbiddenGroup", message = "Group owned by another tenant" });
            }
            else if (group.volumes != null && !group.volumes.All(id => allowedVolumes.Contains(id)))
                return BadRequest(new { error = "forbiddenGroup", message = "Group contains volumes outside your tenants" });

            // verify volumes to remove are subset
            if (p.volumes == null || !p.volumes.All(id => allowedVolumes.Contains(id)))
                return BadRequest(new { error = "volumeNotAllowed", message = "One or more volumes not owned" });

            // perform remove
            var result = await client.SendRequestAsync<RemoveVolumesFromVolumeAccessGroupResult>(
                "RemoveVolumesFromVolumeAccessGroup", new { volumeAccessGroupID, volumes = p.volumes });

            // Normalize attributes before returning
            if (result.volumeAccessGroup.attributes != null)
            {
                result.volumeAccessGroup.attributes = result.volumeAccessGroup.attributes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => NormalizeAttributeValue(kvp.Value)
                );
            }
            return Ok(new { id = 1, result = new { volumeAccessGroup = result.volumeAccessGroup } });
        }
    }

    // DTOs for RPC calls
    public class AddVolumesToVolumeAccessGroupParams
    {
        public int volumeAccessGroupID { get; set; }
        public List<int> volumes { get; set; }
    }

    public class RemoveVolumesFromVolumeAccessGroupParams
    {
        public int volumeAccessGroupID { get; set; }
        public List<int> volumes { get; set; }
    }

    public class ListVolumeAccessGroupsResult
    {
        public List<VolumeAccessGroup> volumeAccessGroups { get; set; }
    }

    public class AddVolumesToVolumeAccessGroupResult
    {
        public VolumeAccessGroup volumeAccessGroup { get; set; }
    }

    public class RemoveVolumesFromVolumeAccessGroupResult
    {
        public VolumeAccessGroup volumeAccessGroup { get; set; }
    }
}
