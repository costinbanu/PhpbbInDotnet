using Microsoft.AspNetCore.Authentication.Cookies;
using System.Linq;
using System.Security.Claims;

namespace PhpbbInDotnet.Domain.Utilities
{
    public static class IdentityUtility
    {
        const string ClaimName = "UserId";

        public static bool TryGetUserId(ClaimsPrincipal claimsPrincipal, out int userId)
        {
            var allClaims = claimsPrincipal.FindAll(ClaimName).ToList();
            if (allClaims.Count == 1 && int.TryParse(allClaims[0].Value, out userId) && userId > 0)
            {
                return true;
            }
            userId = 0;
            return false;
        }

        public static ClaimsPrincipal CreateClaimsPrincipal(int userId)
        {
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimName, userId.ToString()));
            return new ClaimsPrincipal(identity);
        }
    }
}
