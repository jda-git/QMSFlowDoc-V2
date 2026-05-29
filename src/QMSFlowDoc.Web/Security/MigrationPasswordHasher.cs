using Microsoft.AspNetCore.Identity;
using QMSFlowDoc.Domain.Identity;
using BCrypt.Net;

namespace QMSFlowDoc.Web.Security
{
    public class MigrationPasswordHasher : PasswordHasher<ApplicationUser>
    {
        public override PasswordVerificationResult VerifyHashedPassword(
            ApplicationUser user, 
            string hashedPassword, 
            string providedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            {
                return PasswordVerificationResult.Failed;
            }

            // Check if the hash matches BCrypt format (starts with $2a$, $2b$, or $2y$)
            if (hashedPassword.StartsWith("$2a$") || hashedPassword.StartsWith("$2b$") || hashedPassword.StartsWith("$2y$"))
            {
                try
                {
                    bool isBCryptValid = BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
                    if (isBCryptValid)
                    {
                        // Password is correct, but we want to upgrade it to the new Identity PBKDF2 format
                        return PasswordVerificationResult.SuccessRehashNeeded;
                    }
                    
                    return PasswordVerificationResult.Failed;
                }
                catch
                {
                    return PasswordVerificationResult.Failed;
                }
            }

            try
            {
                // Fallback to standard Identity PBKDF2 verification
                return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
            }
            catch (System.FormatException)
            {
                return PasswordVerificationResult.Failed;
            }
        }
    }
}
