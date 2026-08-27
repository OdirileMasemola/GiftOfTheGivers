using System.Security.Cryptography;
using System.Text;

namespace GiftOfTheGivers.Data
{
    public class SeedData
    {
        /// <summary>
        /// Initialize seed data for the custom Users table (not ASP.NET Identity).
        /// Creates demo users if they don't already exist.
        /// Does NOT create ReliefProject data as ReliefProjects table does not exist in Azure.
        /// </summary>
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // Ensure database tables exist
            try
            {
                await context.Database.EnsureCreatedAsync();
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Error ensuring database is created.");
                return;
            }

            // Seed demo users into the custom Users table only if they don't exist
            if (!context.Users.Any(u => u.Email == "employee@test.local"))
            {
                var employeeUser = new User
                {
                    FirstName = "Employee",
                    LastName = "Demo",
                    Email = "employee@test.local",
                    PasswordHash = HashPassword("Employee@123"),
                    PhoneNumber = "+27555000001",
                    Role = "Employee",
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(employeeUser);
            }

            if (!context.Users.Any(u => u.Email == "donor@test.local"))
            {
                var donorUser = new User
                {
                    FirstName = "Donor",
                    LastName = "Demo",
                    Email = "donor@test.local",
                    PasswordHash = HashPassword("Donor@123"),
                    PhoneNumber = "+27555000002",
                    Role = "Donor",
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(donorUser);
            }

            if (!context.Users.Any(u => u.Email == "volunteer@test.local"))
            {
                var volunteerUser = new User
                {
                    FirstName = "Volunteer",
                    LastName = "Demo",
                    Email = "volunteer@test.local",
                    PasswordHash = HashPassword("Volunteer@123"),
                    PhoneNumber = "+27555000003",
                    Role = "Donor", // Volunteers can also donate
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(volunteerUser);
            }

            await context.SaveChangesAsync();

            // Note: ReliefProject seeding removed because ReliefProjects table does not exist in Azure
            // Instead, the application should work with ReliefRequests and ReliefOperations.
            // If demo relief data is needed, it should be created in ReliefRequests/ReliefOperations tables.
        }

        /// <summary>
        /// Hash a password using PBKDF2 with SHA256.
        /// This is a simple implementation; for production, consider using BCrypt or Argon2.
        /// </summary>
        public static string HashPassword(string password)
        {
            const int iterations = 10000;
            const int keySize = 32; // 256 bits
            const int saltSize = 16; // 128 bits

            using (var algorithm = new Rfc2898DeriveBytes(password, saltSize, iterations, HashAlgorithmName.SHA256))
            {
                var key = algorithm.GetBytes(keySize);
                var salt = algorithm.Salt;

                // Combine salt + hash into a single string
                var hashBytes = new byte[saltSize + keySize];
                Array.Copy(salt, 0, hashBytes, 0, saltSize);
                Array.Copy(key, 0, hashBytes, saltSize, keySize);

                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Verify a password against a hash created by HashPassword.
        /// </summary>
        public static bool VerifyPassword(string password, string hash)
        {
            try
            {
                var hashBytes = Convert.FromBase64String(hash);

                // Extract salt from the combined hash
                const int saltSize = 16;
                const int keySize = 32;
                const int iterations = 10000;

                var salt = new byte[saltSize];
                Array.Copy(hashBytes, 0, salt, 0, saltSize);

                using (var algorithm = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                {
                    var key = algorithm.GetBytes(keySize);

                    // Compare computed hash with stored hash
                    for (int i = 0; i < keySize; i++)
                    {
                        if (hashBytes[i + saltSize] != key[i])
                            return false;
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
