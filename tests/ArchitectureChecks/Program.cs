using System.Reflection;
using AppointmentSchedulingBookingSystem;
using AppointmentSchedulingBookingSystem.BLL.Admin;
using AppointmentSchedulingBookingSystem.BLL.Consultants;
using AppointmentSchedulingBookingSystem.BLL.Home;
using AppointmentSchedulingBookingSystem.DAL.Admin;
using AppointmentSchedulingBookingSystem.DAL.Consultants;
using AppointmentSchedulingBookingSystem.DAL.Common;
using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.ViewModels;
using AppointmentSchedulingBookingSystem.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name); checks++;
}

var repository = new ConsultantFake();
var service = new ConsultantService(repository);
var model = await service.GetScheduleAsync("owner");
Check(model?.WorkingDays.Count == 7, "Schedule has seven days");
var invalid = new ConsultantScheduleViewModel { WorkingDays = new() };
Check(!(await service.SaveScheduleAsync("owner", invalid)).Succeeded && repository.Saves == 0,
    "Malformed schedule cannot write");
model!.WorkingDays[0].IsWorking = true;
model.WorkingDays[0].StartTime = new(12, 0); model.WorkingDays[0].EndTime = new(11, 0);
Check(!(await service.SaveScheduleAsync("owner", model)).Succeeded && repository.Saves == 0,
    "Reversed hours cannot write");
model = await service.GetScheduleAsync("owner");
repository.Breaks.Add(new() { Id=1, ConsultantId=1, DayOfWeek=DayOfWeek.Monday, StartTime=new(12,0), EndTime=new(13,0) });
repository.Breaks.Add(new() { Id=2, ConsultantId=1, Date=new(2026,11,23), StartTime=new(14,0), EndTime=new(15,0) });
model!.WorkingDays.Single(d => d.DayOfWeek == DayOfWeek.Monday).IsWorking = false;
Check((await service.SaveScheduleAsync("owner", model)).Succeeded && repository.Breaks.Count == 1 &&
    repository.Breaks[0].Date.HasValue, "Removing working day removes recurring breaks only");
repository.Availability.Add(new() { ConsultantId=1, DayOfWeek=DayOfWeek.Monday, StartTime=new(9,0), EndTime=new(17,0) });
var before = repository.Saves;
Check((await service.AddBreakAsync("owner", new())).Succeeded && repository.Saves == before,
    "No break type is a no-op");
var outside = new ConsultantBreakViewModel { BreakType="Recurring", DayOfWeek=DayOfWeek.Monday, StartTime=new(8,0), EndTime=new(10,0) };
Check(!(await service.AddBreakAsync("owner", outside)).Succeeded && repository.Saves == before,
    "Break outside working hours rejected");
var overlap = new ConsultantBreakViewModel { BreakType="Recurring", DayOfWeek=DayOfWeek.Monday, StartTime=new(14,30), EndTime=new(15,30) };
Check(!(await service.AddBreakAsync("owner", overlap)).Succeeded, "Recurring break checks dated overlaps");
var once = new ConsultantBreakViewModel { BreakType="Specific", Date=new(2026,11,30), StartTime=new(14,30), EndTime=new(15,30) };
Check((await service.AddBreakAsync("owner", once)).Succeeded && repository.Breaks.Last().DayOfWeek == null,
    "One-time break on another date is accepted");
repository.Breaks.Add(new() { Id=99, ConsultantId=2 });
Check((await service.DeleteBreakAsync("owner", 99)).NotFound && repository.Breaks.Any(b => b.Id == 99),
    "Cannot delete another consultant's break");
Check((await service.GetScheduleAsync("missing")) == null, "Missing profile preserved");
var week = await service.GetWeekAsync("owner");
Check(week.Days.Count == 7 && week.WeekStart.DayOfWeek == DayOfWeek.Monday, "Weekly dashboard dates");
var home = new HomeService();
Check(home.GetDashboard(new[] { "Customer", "Admin" }) == "Admin" && home.GetDashboard(Array.Empty<string>()) == null,
    "Role redirect precedence");

var adminRepo = DispatchProxy.Create<IAdminRepository, RepositoryProxy>();
var proxy = (RepositoryProxy)(object)adminRepo;
var customer = new Customer { UserId="customer", Name="Original", Phone="123" };
var roles = new List<string> { "Customer" };
var transaction = new TransactionFake();
int writes = 0;
proxy.Handler = (name, args) => name switch
{
    "FindUserAsync" => Task.FromResult<IdentityUser?>(new IdentityUser { Id="customer" }),
    "GetRolesAsync" => Task.FromResult<IList<string>>(roles),
    "FindCustomerAsync" => Task.FromResult<Customer?>(customer),
    "BeginTransactionAsync" => Task.FromResult<IDataTransaction>(transaction),
    "SaveAsync" => Save(),
    "AddRoleAsync" => Task.FromResult(new[] { "Role failure" }),
    _ => throw new Exception("Unexpected repository call: " + name)
};
Task Save() { writes++; return Task.CompletedTask; }
var admin = new AdminService(adminRepo);
var update = new UpdateUserViewModel { UserId="customer", Name="Changed" };
Check((await admin.UpdateUserAsync(update, new HashSet<string> { "Name" })).Succeeded &&
    customer.Name == "Changed" && customer.Phone == "123" && transaction.Committed,
    "Partial user update preserves omitted phone and commits");
update.Name = "";
Check(!(await admin.UpdateUserAsync(update, new HashSet<string> { "Name" })).Succeeded && writes == 1,
    "Explicit empty name rejected without saving");
roles.Clear(); update.Name="Changed"; update.Role="Customer";
Check(!(await admin.UpdateUserAsync(update, new HashSet<string> { "Role" })).Succeeded && transaction.RolledBack,
    "Role failure rolls back profile transaction");
roles.Add("Admin"); var saved = writes;
Check(!(await admin.UpdateUserAsync(update, new HashSet<string>())).Succeeded && writes == saved,
    "Admin account protection");

// Resolve the actual registrations and MVC actions without connecting to a database.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ApplicationName=typeof(DependencyInjection).Assembly.GetName().Name,
    ContentRootPath=Path.GetFullPath("AppointmentSchedulingBookingSystem")
});
builder.Services.AddDbContext<AppointmentSchedulingBookingSystemIdentityDbContext>(o => o.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ArchitectureChecks;Trusted_Connection=True"));
builder.Services.AddDefaultIdentity<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppointmentSchedulingBookingSystemIdentityDbContext>();
builder.Services.AddApplicationLayers();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(DependencyInjection).Assembly);
builder.Services.AddRazorPages();
await using var app = builder.Build();
using var scope = app.Services.CreateScope();
Check(scope.ServiceProvider.GetRequiredService<IAdminService>() is AdminService &&
    scope.ServiceProvider.GetRequiredService<IConsultantService>() is ConsultantService, "BLL/DAL dependency injection resolves");
var actions = scope.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToList();
Check(new[] { "Home", "Admin", "Customer", "Consultant" }.All(c => actions.Any(a => a.ControllerName == c && a.ActionName == "Index")),
    "Feature folders preserve controller route names");
var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
Check(engine.GetView(null, "/Views/Consultant/Index.cshtml", true).Success &&
    engine.GetView(null, "/Views/Admin/Index.cshtml", true).Success,
    "Compiled Razor views still resolve");
Console.WriteLine($"{checks} regression checks passed.");

public class RepositoryProxy : DispatchProxy
{
    public Func<string, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
}
public class TransactionFake : IDataTransaction
{
    public bool Committed, RolledBack;
    public Task CommitAsync() { Committed=true; return Task.CompletedTask; }
    public Task RollbackAsync() { RolledBack=true; return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
public class ConsultantFake : IConsultantRepository
{
    public Consultant Profile = new() { Id=1, UserId="owner", Name="Consultant", Specialization="General" };
    public List<ConsultantAvailability> Availability = new() { new() { ConsultantId=1, DayOfWeek=DayOfWeek.Monday, StartTime=new(9,0), EndTime=new(17,0) } };
    public List<ConsultantBreak> Breaks = new();
    public int Saves;
    public Task<Consultant?> FindConsultantAsync(string userId) => Task.FromResult(userId == "owner" ? Profile : null);
    public Task<List<ConsultantAvailability>> ReadAvailabilityAsync(int id) => Task.FromResult(Availability.Where(a => a.ConsultantId == id).ToList());
    public Task<List<ConsultantAvailability>> GetAvailabilityAsync(int id) => ReadAvailabilityAsync(id);
    public Task<List<ConsultantBreak>> GetWeekBreaksAsync(int id, DateOnly start, DateOnly end) => Task.FromResult(Breaks.Where(b => b.ConsultantId == id && (b.DayOfWeek != null || b.Date >= start && b.Date <= end)).ToList());
    public Task<List<ConsultantBreak>> GetRecurringBreaksAsync(int id, List<DayOfWeek> days) => Task.FromResult(Breaks.Where(b => b.ConsultantId == id && b.DayOfWeek.HasValue && days.Contains(b.DayOfWeek.Value)).ToList());
    public Task<List<ConsultantBreak>> GetBreaksAsync(int id) => Task.FromResult(Breaks.Where(b => b.ConsultantId == id).ToList());
    public Task<ConsultantAvailability?> FindAvailabilityAsync(int id, DayOfWeek day) => Task.FromResult(Availability.FirstOrDefault(a => a.ConsultantId == id && a.DayOfWeek == day));
    public Task<ConsultantBreak?> FindBreakAsync(int id, int breakId) => Task.FromResult(Breaks.FirstOrDefault(b => b.ConsultantId == id && b.Id == breakId));
    public void RemoveBreaks(IEnumerable<ConsultantBreak> items) { foreach (var item in items.ToList()) Breaks.Remove(item); }
    public void RemoveAvailability(IEnumerable<ConsultantAvailability> items) { foreach (var item in items.ToList()) Availability.Remove(item); }
    public void AddAvailability(ConsultantAvailability item) => Availability.Add(item);
    public void AddBreak(ConsultantBreak item) => Breaks.Add(item);
    public void RemoveBreak(ConsultantBreak item) => Breaks.Remove(item);
    public Task SaveAsync() { Saves++; return Task.CompletedTask; }
}
