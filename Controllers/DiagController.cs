using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace SolidFireGateway.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DiagController : ControllerBase
    {
        /// <summary>
        /// Returns the current authenticated user's name and their Windows group memberships.
        /// </summary>
        [HttpGet("whoami")]
        public IActionResult WhoAmI()
        {
            var user = HttpContext.User;
            var name = user.Identity?.Name;

            // Enumerate Windows groups
            var windowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(windowsIdentity);
            var groups = windowsIdentity.Groups
                .Select(g => {
                    try {
                        return g.Translate(typeof(System.Security.Principal.NTAccount)).ToString();
                    } catch {
                        return g.Value;
                    }
                })
                .ToList();

            return Ok(new { name, groups });
        }
    }
}
