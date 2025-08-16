using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;

// Placeholder HTTP templates for ClusterStatsController:
// GET    /SolidFire/{cluster}/clusterstats/capacity
// GET    /SolidFire/{cluster}/clusterstats/performance

namespace SolidFireGateway.Controllers
{
    [ApiController]
    [Route("SolidFire/{cluster}/clusterstats")]
    public class ClusterStatsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public ClusterStatsController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        private bool IsUserAllowed(string action)
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
                return false;
            // allow super-admin roles defined in configuration
            var globalAdmins = _config.GetSection("GlobalAdminRoles").Get<string[]>() ?? System.Array.Empty<string>();
            if (globalAdmins.Any(r => User.IsInRole(r)))
                return true;
            var roles = _config.GetSection($"ClusterStatsAccess:ActionRoles:{action}").Get<string[]>() ?? System.Array.Empty<string>();
            // allow if user in role
            foreach (var r in roles)
            {
                if (User.IsInRole(r)) return true;
            }
            return false;
        }

        /// <summary>
        /// Gets cluster capacity metrics.
        /// </summary>
        /// <remarks>
        /// Enforces ClusterStatsAccess:Get role-based access.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        [HttpGet("capacity")]
        public async Task<ActionResult<object>> GetClusterCapacity(string cluster)
        {
            try
            {
                if (!IsUserAllowed("Get"))
                    return Forbid();

                var client = new SolidFireClient(
                    _httpClientFactory.CreateClient(cluster), _config, NullLogger<SolidFireClient>.Instance);
                var fullResult = await client.SendRequestAsync<GetClusterCapacityResult>(
                    "GetClusterCapacity", new { });
                return Ok(new { id = 1, result = fullResult });
            }
            catch (HttpRequestException ex) when (ex.Message.StartsWith("JSON-RPC error:"))
            {
                // Propagate JSON-RPC error to client
                return BadRequest(new { id = 1, cluster, error = "rpcError", message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new { id = 1, cluster, error = "unreachable", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets cluster performance statistics.
        /// </summary>
        /// <remarks>
        /// Enforces ClusterStatsAccess:Get role-based access.
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        [HttpGet("performance")]
        public async Task<ActionResult<object>> GetClusterStats(string cluster)
        {
            try
            {
                if (!IsUserAllowed("Get"))
                    return Forbid();

                var client = new SolidFireClient(
                    _httpClientFactory.CreateClient(cluster), _config, NullLogger<SolidFireClient>.Instance);
                var fullResult = await client.SendRequestAsync<GetClusterStatsResult>(
                    "GetClusterStats", new { });
                return Ok(new { id = 1, result = fullResult });
            }
            catch (HttpRequestException ex) when (ex.Message.StartsWith("JSON-RPC error:"))
            {
                return BadRequest(new { id = 1, cluster, error = "rpcError", message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new { id = 1, cluster, error = "unreachable", message = ex.Message });
            }
        }
    }
}
