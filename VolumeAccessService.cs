using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace SolidFireGateway
{
    public class VolumeAccessService
    {
        private readonly IConfiguration _config;
        public VolumeAccessService(IConfiguration config)
        {
            _config = config;
        }

        public List<string> GetActionRoles(string action)
        {
            var roles = new List<string>();
            var section = _config.GetSection($"VolumeAccess:ActionRoles:{action}");
            if (section.Exists())
            {
                foreach (var role in section.GetChildren())
                {
                    roles.Add(role.Value);
                }
            }
            return roles;
        }

        public bool IsUserInRole(string user, List<string> userGroups, string action)
        {
            var allowedRoles = GetActionRoles(action);
            foreach (var group in userGroups)
            {
                if (allowedRoles.Contains(group))
                    return true;
            }
            if (allowedRoles.Contains(user))
                return true;
            return false;
        }
    }
}
