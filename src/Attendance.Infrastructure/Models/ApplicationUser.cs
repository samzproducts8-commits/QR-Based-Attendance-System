using Microsoft.AspNetCore.Identity;

namespace Attendance.Infrastructure.Models;
//mine ASP.NET Core Identity user (login credentials/roles), 
// extended with an optional link to a Staff record for employee-role logins.
public class ApplicationUser : IdentityUser
{
    // Extended identity user for the attendance system.
    // Additional profile properties can be added here as needed
    // (e.g., linked StaffId for Employee-role users).
    public int? StaffId { get; set; }

    // Navigation property
    // system user for login and authentication
    public Staff? Staff { get; set; }
}
