using Brainova.DAL.Data;
using Brainova.DAL.Modles;
using Brainova.DAL.Utilites;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Brainova.DAL.Utilities
{
    public class SeedData : ISeedData
    {
        private readonly AppDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public SeedData(
            AppDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        // 🔹 Apply pending migrations automatically
        public async Task DataSeedingAsync()
        {
            if ((await _context.Database.GetPendingMigrationsAsync()).Any())
            {
                await _context.Database.MigrateAsync();
            }
        }

        // 🔹 Seed roles + users
        public async Task IdentitySeedingAsync()
        {
            // 1️⃣ Seed Roles
            var roles = new List<string>
            {
                "SuperAdmin",
                "Admin",
                "Supervisor",
                "Student"
            };

            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));
            }

            // ✅ Only seed users when the DB has no users at all.
            // This prevents re-seeding when a seeded user's info (email, etc.) is edited.
            if (await _userManager.Users.AnyAsync())
                return;

            // 2️⃣ Seed Supervisor FIRST (so we can assign students to him)
            var supervisor = await CreateUserIfNotExists(
                email: "supervisor@brainova.com",
                username: "DrWayne",
                fullName: "Thomas Wayne",
                phone: "0000000002",
                password: "Supervisor@1234",
                role: "Supervisor"
            );

            // 3️⃣ Seed SuperAdmin
            await CreateUserIfNotExists(
                email: "superadmin@brainova.com",
                username: "AmirEid83",
                fullName: "Amir Eid",
                phone: "0000000000",
                password: "Super@1234",
                role: "SuperAdmin"
            );

            // 4️⃣ Seed Admin
            await CreateUserIfNotExists(
                email: "admin@brainova.com",
                username: "Tul8te",
                fullName: "Mohamed Khafage",
                phone: "0000000001",
                password: "Admin@1234",
                role: "Admin"
            );

            // 5️⃣ Seed Student (WITH SupervisorUserId)
            await CreateStudentIfNotExists(
                email: "student@brainova.com",
                username: "ElRoumi0",
                fullName: "Majeda ElRoumi",
                phone: "0000000003",
                password: "Student@1234",
                supervisorUserId: supervisor.Id
            );
        }

        // 🔹 Normal users (SuperAdmin/Admin/Supervisor)
        private async Task<ApplicationUser> CreateUserIfNotExists(
            string email,
            string username,
            string fullName,
            string phone,
            string password,
            string role)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
                return user;

            user = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                UserName = username,
                PhoneNumber = phone,
                EmailConfirmed = true,
                IsBlocked = false,
                SupervisorUserId = null // ✅ non-students have null
            };

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
                throw new Exception(string.Join(";", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, role);
            return user;
        }

        // 🔹 Student must have a supervisor
        private async Task<ApplicationUser> CreateStudentIfNotExists(
            string email,
            string username,
            string fullName,
            string phone,
            string password,
            string supervisorUserId)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                // ✅ If student exists but SupervisorUserId is null, fix it
                if (string.IsNullOrWhiteSpace(user.SupervisorUserId))
                {
                    user.SupervisorUserId = supervisorUserId;
                    await _userManager.UpdateAsync(user);
                }
                return user;
            }

            user = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                UserName = username,
                PhoneNumber = phone,
                EmailConfirmed = true,
                IsBlocked = false,
                SupervisorUserId = supervisorUserId // ✅ REQUIRED
            };

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
                throw new Exception(string.Join(";", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Student");
            return user;
        }
    }
}
