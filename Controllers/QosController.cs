using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

// Placeholder HTTP templates for QosController:
// GET    /SolidFire/{cluster}/qospolicies
// GET    /SolidFire/{cluster}/qospolicies/{policyID}
// PUT    /SolidFire/{cluster}/qospolicies/{policyID}
// DELETE /SolidFire/{cluster}/qospolicies/{policyID}

namespace SolidFireGateway.Controllers
{
    [ApiController]
    [Route("SolidFire/{cluster}/qospolicies")]
    public class QosController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public QosController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        private List<string> GetUserGroups()
        {
            var sids = User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.GroupSid)
                .Select(c => c.Value);
            var names = new List<string>();
            foreach (var sid in sids)
            {
                try
                {
                    names.Add(new System.Security.Principal.SecurityIdentifier(sid)
                        .Translate(typeof(System.Security.Principal.NTAccount)).ToString());
                }
                catch { }
            }
            return names;
        }

        private bool IsUserInRole(string action)
        {
            var user = User.Identity?.Name ?? string.Empty;
            var groups = GetUserGroups();
            // Super-admin roles bypass based on configuration
            var globalAdmins = _config.GetSection("GlobalAdminRoles").Get<string[]>() ?? System.Array.Empty<string>();
            if (globalAdmins.Any(r => User.IsInRole(r))) return true;
            var roles = _config
                .GetSection($"QosAccess:ActionRoles:{action}")
                .Get<string[]>() ?? Array.Empty<string>();
            if (roles.Any(r => groups.Contains(r))) return true;
            if (roles.Contains(user)) return true;
            return false;
        }

        /// <summary>
        /// Lists all QoS policies.
        /// </summary>
        /// <remarks>
        /// Enforces QosAccess:List role-based access. Trims detailed curve and burstTime data.
        /// </remarks>
        [HttpGet]
        public async Task<ActionResult<object>> ListQosPolicies(string cluster)
        {
            if (!IsUserInRole("List")) return Forbid();
            var httpClient = _httpClientFactory.CreateClient(cluster);
            var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);
            var fullResult = await client.SendRequestAsync<ListQoSPoliciesResult>("ListQoSPolicies", new { });
            // Trim QoS details to avoid curve and burstTime
            var trimmed = fullResult.qosPolicies.Select(q => new {
                name = q.name,
                qosPolicyID = q.qosPolicyID,
                qos = new {
                    minIOPS = q.qos.minIOPS,
                    maxIOPS = q.qos.maxIOPS,
                    burstIOPS = q.qos.burstIOPS
                },
                volumeIDs = q.volumeIDs
            }).ToList();
            return Ok(new { id = 1, result = new { qosPolicies = trimmed } });
        }

        /// <summary>
        /// Gets a specific QoS policy by ID.
        /// </summary>
        /// <remarks>
        /// Enforces QosAccess:Get role-based access and trims detailed curve and burstTime data.
        /// </remarks>
        [HttpGet("{policyID:int}")]
        public async Task<ActionResult<object>> GetQosPolicy(string cluster, int policyID)
        {
            if (!IsUserInRole("Get")) return Forbid();
            var httpClient = _httpClientFactory.CreateClient(cluster);
            var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);
            var fullResult = await client.SendRequestAsync<GetQoSPolicyResult>("GetQoSPolicy", new { qosPolicyID = policyID });
            // Trim QoS details
            var q = fullResult.qosPolicy;
            var trimmed = new {
                name = q.name,
                qosPolicyID = q.qosPolicyID,
                qos = new {
                    minIOPS = q.qos.minIOPS,
                    maxIOPS = q.qos.maxIOPS,
                    burstIOPS = q.qos.burstIOPS
                },
                volumeIDs = q.volumeIDs
            };
            return Ok(new { id = 1, result = new { qosPolicy = trimmed } });
        }

        /// <summary>
        /// Modifies an existing QoS policy.
        /// </summary>
        /// <remarks>
        /// Enforces QosAccess:Update role-based access.
        /// </remarks>
        [HttpPut("{policyID:int}")]
        public async Task<ActionResult<object>> ModifyQosPolicy(string cluster, int policyID,
            [FromBody] ModifyQoSPolicyParams modifyParams)
        {
            if (!IsUserInRole("Update")) return Forbid();
            modifyParams.qosPolicyID = policyID;
            var httpClient = _httpClientFactory.CreateClient(cluster);
            var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);
            var fullResult = await client.SendRequestAsync<ModifyQoSPolicyResult>("ModifyQoSPolicy", modifyParams);
            // Trim QoS details
            var q = fullResult.qosPolicy;
            var trimmed = new {
                name = q.name,
                qosPolicyID = q.qosPolicyID,
                qos = new {
                    minIOPS = q.qos.minIOPS,
                    maxIOPS = q.qos.maxIOPS,
                    burstIOPS = q.qos.burstIOPS
                },
                volumeIDs = q.volumeIDs
            };
            return Ok(new { id = 1, result = new { qosPolicy = trimmed } });
        }

        /// <summary>
        /// Deletes a QoS policy.
        /// </summary>
        /// <remarks>
        /// Enforces QosAccess:Delete role-based access.
        /// </remarks>
        [HttpDelete("{policyID:int}")]
        public async Task<ActionResult<object>> DeleteQosPolicy(string cluster, int policyID)
        {
            if (!IsUserInRole("Delete")) return Forbid();
            var httpClient = _httpClientFactory.CreateClient(cluster);
            var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);
            var parameters = new { qosPolicyID = policyID };
            await client.SendRequestAsync<object>("DeleteQoSPolicy", parameters);
            return Ok(new { id = 1, result = new { } });
        }
    }
}
