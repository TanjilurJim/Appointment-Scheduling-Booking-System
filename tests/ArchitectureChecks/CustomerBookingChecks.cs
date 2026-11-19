using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using AppointmentSchedulingBookingSystem;
using AppointmentSchedulingBookingSystem.Areas.Identity.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AppointmentSchedulingBookingSystem.BLL.Customers;
using AppointmentSchedulingBookingSystem.DAL.Customers;
using AppointmentSchedulingBookingSystem.DAL.Common;
using AppointmentSchedulingBookingSystem.Models;
using AppointmentSchedulingBookingSystem.ViewModels;

public static class CustomerBookingChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var f = new Fixture();
        var page = await f.Service.GetBookingPageAsync("customer", f.Date);
        check(page.HasCustomerProfile && page.Consultants.Single().Slots.Count == 4 &&
            page.Consultants.Single().Slots.All(s => s.IsAvailable), "Customer: slots follow working hours and duration");
        f.Consultant.Breaks.Add(new() { DayOfWeek=f.Date.DayOfWeek, StartTime=new(9,15), EndTime=new(9,45) });
        var slots = (await f.Service.GetBookingPageAsync("customer", f.Date)).Consultants.Single().Slots;
        check(!slots[0].IsAvailable && !slots[1].IsAvailable && slots[2].IsAvailable,
            "Customer: partial break overlaps block both slots, adjacent slots stay free");
        check(!(await f.Service.BookAsync("customer", f.Request(9,0))).Succeeded && f.Repository.Saved.Count == 0,
            "Customer: break cannot be bypassed by POST");
        f.Consultant.Breaks.Clear();
        f.Consultant.Breaks.Add(new() { Date=f.Date.AddDays(7), StartTime=new(9,0), EndTime=new(10,0) });
        check((await f.Service.GetBookingPageAsync("customer", f.Date)).Consultants.Single().Slots[0].IsAvailable,
            "Customer: dated break only blocks its exact date");
        f.Consultant.Appointments.Add(new() { Date=f.Date, StartTime=new(9,45), EndTime=new(10,15), Status=AppointmentStatus.Confirmed });
        slots = (await f.Service.GetBookingPageAsync("customer", f.Date)).Consultants.Single().Slots;
        check(!slots[1].IsAvailable && !slots[2].IsAvailable && slots[3].IsAvailable,
            "Customer: appointment overlap uses full interval, not just start time");
        f.Consultant.Appointments.Single().Status=AppointmentStatus.Cancelled;
        check((await f.Service.GetBookingPageAsync("customer", f.Date)).Consultants.Single().Slots.All(s => s.IsAvailable),
            "Customer: cancelled appointment releases slots");
        f.Repository.Own.Add(new() { CustomerId=7, ConsultantId=2, Date=f.Date, StartTime=new(10,0), EndTime=new(10,30), Consultant=new() { Name="Other" } });
        check(!(await f.Service.BookAsync("customer", f.Request(10,0))).Succeeded && f.Repository.Saved.Count == 0,
            "Customer: cannot book simultaneous appointments with different consultants");
        check(!(await f.Service.BookAsync("customer", f.Request(9,10))).Succeeded,
            "Customer: off-grid start time rejected");
        check(!(await f.Service.BookAsync("customer", new() { ConsultantId=1 })).Succeeded,
            "Customer: missing date and time rejected");
        check(!(await f.Service.BookAsync("missing", f.Request(9,0))).Succeeded,
            "Customer: missing profile cannot book");
        check(!(await f.Service.BookAsync("customer", new() { ConsultantId=999, Date=f.Date, StartTime=new(9,0) })).Succeeded,
            "Customer: unknown consultant rejected");
        f.Consultant.IsActive=false;
        check(!(await f.Service.BookAsync("customer", f.Request(9,0))).Succeeded,
            "Customer: inactive consultant cannot be booked");
        f.Consultant.IsActive=true;
        f.Consultant.Availabilities.Single().EndTime=new(10,45);
        check((await f.Service.GetBookingPageAsync("customer", f.Date)).Consultants.Single().Slots.Count == 3,
            "Customer: incomplete last slot omitted");
        f.Consultant.Availabilities.Single().StartTime=new(23,0);f.Consultant.Availabilities.Single().EndTime=new(23,59);
        check((await f.Service.GetBookingPageAsync("customer", f.Date)).Consultants.Single().Slots.Count == 1,
            "Customer: slot arithmetic does not wrap midnight");
        f = new Fixture();
        await f.Service.GetBookingPageAsync("customer", f.Date);
        f.Consultant.Appointments.Add(new() { Date=f.Date, StartTime=new(9,0), EndTime=new(9,30) });
        check(!(await f.Service.BookAsync("customer", f.Request(9,0))).Succeeded && f.Repository.LastTransaction!.Disposed,
            "Customer: stale page rechecks appointment availability and disposes transaction");
        f = new Fixture();
        await f.Service.GetBookingPageAsync("customer", f.Date);
        f.Clock.Now = new DateTimeOffset(2030,1,7,3,1,0,TimeSpan.Zero);
        check(!(await f.Service.BookAsync("customer", f.Request(9,0))).Succeeded,
            "Customer: slot that passed since page load cannot be booked");
        f = new Fixture();
        var yesterday = f.Request(9,0);yesterday.Date=f.Date.AddDays(-1);
        check(!(await f.Service.BookAsync("customer", yesterday)).Succeeded, "Customer: past date rejected");
        var result=await f.Service.BookAsync("customer", f.Request(9,0));
        var saved=f.Repository.Saved.Single();
        check(result.Succeeded && saved.CustomerId == 7 && saved.ConsultantId == 1 && saved.EndTime == new TimeOnly(9,30)
            && saved.Status == AppointmentStatus.Confirmed && f.Repository.LastTransaction!.Committed,
            "Customer: booking uses authenticated customer, derived end time and commits confirmation");
        check(!(await f.Service.BookAsync("customer", f.Request(9,0))).Succeeded && f.Repository.Saved.Count == 1,
            "Customer: resubmitting a booked slot does not insert twice");
        var refreshed=await f.Service.GetBookingPageAsync("customer", f.Date);
        check(refreshed.Appointments.Count == 1 && !refreshed.Consultants.Single().Slots[0].IsAvailable,
            "Customer: confirmation appears on refreshed page with slot unavailable");
        f = new Fixture();f.Repository.Conflict=true;
        result=await f.Service.BookAsync("customer", f.Request(9,0));
        check(!result.Succeeded && result.Errors.Single().Contains("Availability changed") &&
            f.Repository.LastTransaction is { Committed:false, Disposed:true },
            "Customer: competing write produces retry message and no commit");
    }

    public static async Task RunHttpChecks(Action<bool, string> check)
    {
        var fixture = new Fixture();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName=typeof(DependencyInjection).Assembly.GetName().Name,
            ContentRootPath=Path.GetFullPath("AppointmentSchedulingBookingSystem"),
            EnvironmentName="Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppointmentSchedulingBookingSystemIdentityDbContext>(o =>
            o.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=UnusedHttpChecks;Trusted_Connection=True"));
        builder.Services.AddDefaultIdentity<IdentityUser>().AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppointmentSchedulingBookingSystemIdentityDbContext>();
        builder.Services.AddApplicationLayers();
        // Only this test host overrides authentication and DAL. No real database is accessed.
        builder.Services.AddSingleton<ICustomerRepository>(fixture.Repository);
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        builder.Services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme="TestCustomer";
            o.DefaultChallengeScheme="TestCustomer";
            o.DefaultForbidScheme="TestCustomer";
        }).AddScheme<AuthenticationSchemeOptions, TestCustomerAuth>("TestCustomer", _ => { });
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(DependencyInjection).Assembly);
        builder.Services.AddRazorPages();
        await using var app=builder.Build();
        app.UseAuthentication();app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();
        await app.StartAsync();
        using var client=new HttpClient(new HttpClientHandler { CookieContainer=new CookieContainer() });
        client.BaseAddress=new Uri(app.Urls.Single());
        var response=await client.GetAsync("/Customer?date=2030-01-07");
        var html=await response.Content.ReadAsStringAsync();
        check(response.IsSuccessStatusCode && html.Contains("Test Consultant") && html.Contains("Book Selected Slot") && html.Contains("09:00"),
            "HTTP: customer page renders schedule and bookable slots");
        var tokenMatch=Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        check(tokenMatch.Success, "HTTP: booking form has antiforgery token");
        var values=new Dictionary<string,string>
        {
            ["ConsultantId"]="1",["Date"]="2030-01-07",["StartTime"]="09:00:00",
            ["CustomerId"]="999",["EndTime"]="23:00:00"
        };
        var rejected=await client.PostAsync("/Customer/Book",new FormUrlEncodedContent(values));
        check(rejected.StatusCode == HttpStatusCode.BadRequest && fixture.Repository.Saved.Count == 0,
            "HTTP: missing antiforgery token rejected");
        values["__RequestVerificationToken"]=WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
        var booked=await client.PostAsync("/Customer/Book",new FormUrlEncodedContent(values));
        html=await booked.Content.ReadAsStringAsync();
        check(booked.IsSuccessStatusCode && html.Contains("Appointment confirmed") && html.Contains("Unavailable: Booked") &&
            fixture.Repository.Saved.Single().CustomerId == 7 && fixture.Repository.Saved.Single().EndTime == new TimeOnly(9,30),
            "HTTP: book, redirect and confirmation ignore forged customer/end time");
        values["StartTime"]="invalid";
        var invalid=await client.PostAsync("/Customer/Book",new FormUrlEncodedContent(values));
        check(invalid.IsSuccessStatusCode && fixture.Repository.Saved.Count == 1,
            "HTTP: malformed booking time does not save");
        client.DefaultRequestHeaders.Add("X-Test-Role","Admin");
        var forbidden=await client.PostAsync("/Customer/Book",new FormUrlEncodedContent(values));
        check(forbidden.StatusCode == HttpStatusCode.Forbidden, "HTTP: booking requires Customer role");
        await app.StopAsync();
    }

    private sealed class Fixture
    {
        public DateOnly Date = new(2030,1,7);
        public Consultant Consultant = new() { Id=1, Name="Test Consultant", Specialization="General", IsActive=true, SlotDurationMinutes=30 };
        public TestClock Clock = new();
        public CustomerFake Repository;
        public CustomerService Service;
        public Fixture()
        {
            Consultant.Availabilities.Add(new() { ConsultantId=1, DayOfWeek=Date.DayOfWeek, StartTime=new(9,0), EndTime=new(11,0) });
            Repository=new CustomerFake(Consultant); Service=new CustomerService(Repository, Clock);
        }
        public BookAppointmentViewModel Request(int hour,int minute) => new() { ConsultantId=1, Date=Date, StartTime=new(hour,minute) };
    }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = new(2030,1,7,2,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class CustomerFake(Consultant consultant) : ICustomerRepository
    {
        public List<Appointment> Own = new(), Saved = new();
        public Transaction? LastTransaction;
        public bool Conflict;
        public Task<Customer?> FindCustomerAsync(string id) => Task.FromResult<Customer?>(id == "customer" ? new() { Id=7, UserId=id } : null);
        public Task<List<Consultant>> GetSchedulesAsync(DateOnly date,int? id=null) => Task.FromResult(
            consultant.IsActive && (id == null || id == consultant.Id) ? new List<Consultant> { consultant } : new());
        public Task<List<Appointment>> GetCustomerAppointmentsAsync(int id,DateOnly from,DateOnly to) =>
            Task.FromResult(Own.Where(a => a.CustomerId == id && a.Date >= from && a.Date <= to).ToList());
        public Task<IDataTransaction> BeginBookingAsync()
        {
            LastTransaction=new Transaction();return Task.FromResult<IDataTransaction>(LastTransaction);
        }
        public Task AddAppointmentAsync(Appointment appointment)
        {
            if (LastTransaction == null || LastTransaction.Disposed) throw new Exception("Write outside transaction");
            if (Conflict) throw new BookingConflictException(new Exception("Simulated competing insert"));
            appointment.Consultant=consultant;
            Saved.Add(appointment);Own.Add(appointment);consultant.Appointments.Add(appointment);return Task.CompletedTask;
        }
    }
    private sealed class Transaction : IDataTransaction
    {
        public bool Committed, Disposed;
        public Task CommitAsync() { Committed=true;return Task.CompletedTask; }
        public Task RollbackAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() { Disposed=true;return ValueTask.CompletedTask; }
    }
}

public sealed class TestCustomerAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role=Request.Headers["X-Test-Role"].FirstOrDefault() ?? "Customer";
        var identity=new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier,"customer"), new Claim(ClaimTypes.Name,"Test Customer"),
            new Claim(ClaimTypes.Role,role)
        }, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));
    }
}
