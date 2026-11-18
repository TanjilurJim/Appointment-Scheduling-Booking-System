
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AppointmentSchedulingBookingSystem.Areas.Identity.Data;

var builder = WebApplication.CreateBuilder(args);

// Database connection
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

// Register Entity Framework Core
builder.Services.AddDbContext<AppointmentSchedulingBookingSystemIdentityDbContext>(
    options => options.UseSqlServer(connectionString));

// Configure ASP.NET Core Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppointmentSchedulingBookingSystemIdentityDbContext>();

// Add MVC services
builder.Services.AddControllersWithViews();

// Identity uses Razor Pages
builder.Services.AddRazorPages();

var app = builder.Build();

// HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

// Authentication must run before authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// MVC routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Identity Razor Pages
app.MapRazorPages();

app.Run();
