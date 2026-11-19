using AppointmentSchedulingBookingSystem.BLL.Admin;
using AppointmentSchedulingBookingSystem.BLL.Consultants;
using AppointmentSchedulingBookingSystem.BLL.Home;
using AppointmentSchedulingBookingSystem.DAL.Admin;
using AppointmentSchedulingBookingSystem.DAL.Consultants;
namespace AppointmentSchedulingBookingSystem;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationLayers(this IServiceCollection services)
    {
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IConsultantRepository, ConsultantRepository>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IConsultantService, ConsultantService>();
        services.AddScoped<IHomeService, HomeService>();
        return services;
    }
}
