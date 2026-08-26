using Microsoft.AspNetCore.Identity;

namespace GiftOfTheGivers.Data
{
    public class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            // Ensure roles exist
            string[] roleNames = { "Employee", "Donor" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Create test Employee user
            var employeeEmail = "employee@test.local";
            var employeeUser = await userManager.FindByEmailAsync(employeeEmail);
            if (employeeUser == null)
            {
                employeeUser = new IdentityUser
                {
                    UserName = employeeEmail,
                    Email = employeeEmail,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(employeeUser, "Employee@123");
            }

            if (!await userManager.IsInRoleAsync(employeeUser, "Employee"))
            {
                await userManager.AddToRoleAsync(employeeUser, "Employee");
            }

            // Create test Donor user
            var donorEmail = "donor@test.local";
            var donorUser = await userManager.FindByEmailAsync(donorEmail);
            if (donorUser == null)
            {
                donorUser = new IdentityUser
                {
                    UserName = donorEmail,
                    Email = donorEmail,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(donorUser, "Donor@123");
                await userManager.AddToRoleAsync(donorUser, "Donor");
            }

            // Create sample relief projects
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            if (!context.ReliefProjects.Any())
            {
                var projects = new[]
                {
                    new ReliefProject
                    {
                        Name = "Flood Relief - KwaZulu-Natal",
                        Location = "KwaZulu-Natal",
                        Description = "Emergency relief efforts for flood-affected communities.",
                        Status = "Active",
                        StartDate = DateTime.Now.AddMonths(-2),
                        CreatedDate = DateTime.Now.AddMonths(-2)
                    },
                    new ReliefProject
                    {
                        Name = "Drought Assistance Program",
                        Location = "Limpopo",
                        Description = "Water supply and food distribution in drought-stricken areas.",
                        Status = "Active",
                        StartDate = DateTime.Now.AddMonths(-1),
                        CreatedDate = DateTime.Now.AddMonths(-1)
                    },
                    new ReliefProject
                    {
                        Name = "Emergency Medical Support",
                        Location = "Eastern Cape",
                        Description = "Medical supplies and healthcare support for affected regions.",
                        Status = "Planning",
                        StartDate = DateTime.Now.AddDays(7),
                        CreatedDate = DateTime.Now
                    }
                };

                foreach (var project in projects)
                {
                    context.ReliefProjects.Add(project);
                }
                await context.SaveChangesAsync();
            }

            if (!context.Volunteers.Any())
            {
                context.Volunteers.AddRange(
                    new Volunteer
                    {
                        Name = "Sarah Johnson",
                        Email = "sarah.johnson@test.local",
                        Skills = "Medical, First Aid",
                        Availability = "Weekends",
                        RegistrationDate = DateTime.Now.AddDays(-1),
                        Status = "Pending"
                    },
                    new Volunteer
                    {
                        Name = "Marcus Chen",
                        Email = "marcus.chen@test.local",
                        Skills = "Construction, Logistics",
                        Availability = "Full-time Available",
                        RegistrationDate = DateTime.Now.AddDays(-2),
                        Status = "Pending"
                    },
                    new Volunteer
                    {
                        Name = "Emma Williams",
                        Email = "emma.williams@test.local",
                        Skills = "Education, Counselling",
                        Availability = "Evenings",
                        RegistrationDate = DateTime.Now.AddDays(-3),
                        Status = "Approved"
                    },
                    new Volunteer
                    {
                        Name = "David Okonkwo",
                        Email = "david.okonkwo@test.local",
                        Skills = "Translation, Administration",
                        Availability = "Weekends",
                        RegistrationDate = DateTime.Now.AddDays(-4),
                        Status = "Active"
                    }
                );
                await context.SaveChangesAsync();
            }

            if (!context.Donations.Any())
            {
                context.Donations.AddRange(
                    new Donation
                    {
                        DonorName = "Thandi Nkosi",
                        DonorEmail = "donor@test.local",
                        Amount = 5000,
                        Currency = "ZAR",
                        DonationType = "OneTime",
                        DonationDate = DateTime.Now.AddDays(-1),
                        CertificateNumber = $"CERT-{DateTime.Now:yyyyMMdd}-A1B2C3D4"
                    },
                    new Donation
                    {
                        DonorName = "James Peterson",
                        DonorEmail = "james.peterson@test.local",
                        Amount = 1500,
                        Currency = "ZAR",
                        DonationType = "Recurring",
                        RecurringFrequency = "Monthly",
                        DonationDate = DateTime.Now.AddDays(-3),
                        CertificateNumber = $"CERT-{DateTime.Now.AddDays(-3):yyyyMMdd}-E5F6G7H8"
                    },
                    new Donation
                    {
                        DonorName = "Amina Patel",
                        DonorEmail = "amina.patel@test.local",
                        Amount = 2500,
                        Currency = "ZAR",
                        DonationType = "OneTime",
                        DonationDate = DateTime.Now.AddDays(-8),
                        CertificateNumber = $"CERT-{DateTime.Now.AddDays(-8):yyyyMMdd}-I9J0K1L2"
                    },
                    new Donation
                    {
                        DonorName = "Lerato Mokoena",
                        DonorEmail = "lerato.mokoena@test.local",
                        Amount = 750,
                        Currency = "ZAR",
                        DonationType = "Recurring",
                        RecurringFrequency = "Quarterly",
                        DonationDate = DateTime.Now.AddDays(-20),
                        CertificateNumber = $"CERT-{DateTime.Now.AddDays(-20):yyyyMMdd}-M3N4O5P6"
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}
