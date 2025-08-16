using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using SolidFireGateway;

// Placeholder HTTP templates for AccountsController:
// GET    /SolidFire/{cluster}/accounts/efficiency?accountID={accountID}
// GET    /SolidFire/{cluster}/accounts/volumestats?accountID={accountID}

namespace SolidFireGateway.Controllers
{
    [ApiController]
    [Route("SolidFire/{cluster}/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly TenantOptions _tenantOptions;

        public AccountsController(IHttpClientFactory httpClientFactory, IConfiguration config, IOptions<TenantOptions> tenantOptions)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _tenantOptions = tenantOptions.Value;
        }

        private List<string> GetUserGroups()
        {
            var names = new List<string>();
            var sids = User.Claims.Where(c => c.Type == ClaimTypes.GroupSid).Select(c => c.Value);
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
            if (!(User.Identity?.IsAuthenticated ?? false))
                return false;
            // Super-admin roles bypass based on configuration
            var globalAdmins = _config.GetSection("GlobalAdminRoles").Get<string[]>() ?? Array.Empty<string>();
            if (globalAdmins.Any(r => User.IsInRole(r)))
                return true;
            var roles = _config.GetSection($"AccountAccess:ActionRoles:{action}").Get<string[]>() ?? Array.Empty<string>();
            var groups = GetUserGroups();
            if (roles.Any(r => groups.Contains(r)))
                return true;
            if (roles.Contains(User.Identity.Name!))
                return true;
            return false;
        }

        // ListAccounts is currently disabled on front-end to avoid exposing account secrets

        /// <summary>
        /// Retrieves storage efficiency metrics for an account.
        /// </summary>
        /// <remarks>
        /// Enforces tenant scoping and AccountStatsAccess:Get role-based access.
        /// </remarks>
        /// <param name="cluster">Name of the SolidFire cluster (e.g., "DR").</param>
        /// <param name="accountID">Tenant account ID to retrieve efficiency metrics for.</param>
        [HttpGet("efficiency")]
        public async Task<ActionResult<object>> GetAccountEfficiency(string cluster, [FromQuery] int accountID)
        {
            // Tenant-based access
            if (!_tenantOptions.AllowedTenants.Contains(accountID))
                return Forbid();
            // Role-based access: AccountStatsAccess:Get
            var statsRoles = _config.GetSection("AccountStatsAccess:ActionRoles:Get").Get<string[]>() ?? Array.Empty<string>();
            if (!statsRoles.Any(r => User.IsInRole(r)))
                return Forbid();
            var httpClient = _httpClientFactory.CreateClient(cluster);
            var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);
            var fullResult = await client.SendRequestAsync<AccountEfficiencyResult>("GetAccountEfficiency", new { accountID });
            return Ok(new { id = 1, result = fullResult });
        }

        /// <summary>
        /// Lists volume statistics for a given account.
        /// </summary>
        /// <remarks>
        /// Enforces tenant scoping and AccountStatsAccess:List role-based access, which means unless the user is in a role that allows this action,
        /// they will be forbidden from accessing this endpoint. 
        /// </remarks>
        /// <param name="cluster">Cluster identifier from route (e.g., "DR").</param>
        /// <param name="accountID">Tenant account ID to list volume stats for.</param>
        [HttpGet("volumestats")]
        public async Task<ActionResult<object>> ListVolumeStats(string cluster, [FromQuery] int accountID)
        {
            if (!_tenantOptions.AllowedTenants.Contains(accountID))
                return Forbid();
            // Role-based access: AccountStatsAccess:List
            var statsListRoles = _config.GetSection("AccountStatsAccess:ActionRoles:List").Get<string[]>() ?? Array.Empty<string>();
            if (!statsListRoles.Any(r => User.IsInRole(r)))
                return Forbid();
            var httpClient = _httpClientFactory.CreateClient(cluster);
            var client = new SolidFireClient(httpClient, _config, NullLogger<SolidFireClient>.Instance);
            var fullResult = await client.SendRequestAsync<ListVolumeStatsByAccountResult>(
                "ListVolumeStatsByAccount", new { includeVirtualVolumes = false, accounts = new int[] { accountID } });
            return Ok(new { id = 1, result = fullResult });
        }

    }
}
