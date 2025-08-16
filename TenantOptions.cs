using System.Collections.Generic;

namespace SolidFireGateway
{
    /// <summary>
    /// Configuration for permitted tenant account IDs.
    /// </summary>
    public class TenantOptions
    {
        /// <summary>
        /// List of accountIDs that this gateway will manage.
        /// </summary>
        public List<int> AllowedTenants { get; set; } = new List<int>();
    }
}
