Gift of the givers – Disaster Relief Platform
A web application for a disaster relief organisation to manage volunteers, track donations, and coordinate relief operations. Built with ASP.NET Core Razor Page

What this project does?
- This project involves dual sides that include the public site and the staff/donors dashboard.
•	Public Site: Users get information about the organization, donate and enroll for volunteering programs.
•	Staff/Donor Dashboard: Authorized users can control relief operations, manage volunteer applications, and monitor the total donation according to their respective roles.
Tech Stack
In this particular project, different layers and technologies have been utilized and now I will be discussing them below:
•	Web application framework: ASP.NET Core 8.0(Razor Pages)
•	Database: SQL Server using Entity Framework Core.
•	Authorization: ASP.NET Core Identity like email/password/role-based.
•	Web page front end: Razor(.cshtml), CSS and Javascript.

Project Structure
GiftOfTheGivers/
├── Areas/Identity/         # Login/registration pages (ASP.NET Identity UI)
├── Data/
│   ├── ApplicationDbContext.cs   # Database context + entity models
│   ├── SeedData.cs               # Creates demo users/roles/sample data on startup
│   └── Migrations/               # EF Core database migrations
├── Pages/
│   ├── Index.cshtml        # Home page
│   ├── About.cshtml        # About the organisation
│   ├── Donate.cshtml       # Public donation form
│   ├── Volunteer.cshtml    # Public volunteer sign-up form
│   ├── Login.cshtml        # Sign in (incl. demo account buttons)
│   └── Dashboards/         # Role-restricted staff/donor/volunteer dashboards
├── Program.cs              # App startup, services, middleware pipeline
└── appsettings.json        # Configuration (connection string, logging)

User Roles
The various users will have varied roles and access privileges. The roles and access privileges of each of the users will be elaborated below:
•	Employee: Full dashboard access, manages relief operations like reviewing and approving volunteers, and viewing donations.
•	Donor: Donor dashboard shows own donations.
•	User: Access to home page, about page, Donate, Volunteer signup, Login/Register.
