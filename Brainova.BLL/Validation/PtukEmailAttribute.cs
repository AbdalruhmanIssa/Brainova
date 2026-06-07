using System.ComponentModel.DataAnnotations;

namespace Brainova.BLL.Validation
{
    /// <summary>
    /// Restricts registration emails to the university domain.
    /// Only addresses whose host is "ptuk.edu.ps" or a sub-domain of it
    /// (e.g. "students.ptuk.edu.ps") are accepted.
    /// Examples: N.Salman@ptuk.edu.ps, a.y.issa@students.ptuk.edu.ps
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class PtukEmailAttribute : ValidationAttribute
    {
        // The common part every allowed email must end with (after the '@').
        private const string AllowedDomain = "ptuk.edu.ps";

        public PtukEmailAttribute()
        {
            ErrorMessage = "Email must be a PTUK university address";
        }

        public override bool IsValid(object? value)
        {
            // Let [Required] handle null/empty. Skip validation when empty.
            if (value is null)
                return true;

            var email = value.ToString();
            if (string.IsNullOrWhiteSpace(email))
                return true;

            var atIndex = email.LastIndexOf('@');
            if (atIndex <= 0 || atIndex == email.Length - 1)
                return false;

            var host = email[(atIndex + 1)..].Trim().ToLowerInvariant();

            // Accept exactly "ptuk.edu.ps" or any sub-domain "*.ptuk.edu.ps".
            return host == AllowedDomain || host.EndsWith("." + AllowedDomain);
        }
    }
}
