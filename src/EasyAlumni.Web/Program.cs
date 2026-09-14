using EasyAlumni.Core.Entities;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Ensure OpenSSL configuration for SQL Server 2012 TLS compatibility on Linux
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENSSL_CONF")))
{
    var localCnf = Path.Combine(AppContext.BaseDirectory, "openssl.cnf");
    if (File.Exists(localCnf))
    {
        Environment.SetEnvironmentVariable("OPENSSL_CONF", localCnf);
    }
    else if (File.Exists("/home/ai/project/SCSchooling/openssl.cnf"))
    {
        Environment.SetEnvironmentVariable("OPENSSL_CONF", "/home/ai/project/SCSchooling/openssl.cnf");
    }
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.UseCompatibilityLevel(110); // SQL Server 2012 compatibility level
        sqlOptions.EnableRetryOnFailure(3);
    }));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

builder.Services.AddHttpClient();
builder.Services.AddScoped<EasyAlumni.Core.Interfaces.IFileStorageService, EasyAlumni.Infrastructure.Services.LocalFileStorageService>();
builder.Services.AddScoped<EasyAlumni.Core.Interfaces.IQrCodeService, EasyAlumni.Infrastructure.Services.QrCodeService>();
builder.Services.AddScoped<EasyAlumni.Core.Interfaces.ISmsService, EasyAlumni.Infrastructure.Services.ConfigurableSmsService>();
builder.Services.AddScoped<EasyAlumni.Core.Interfaces.IWhatsAppService, EasyAlumni.Infrastructure.Services.WhatsAppService>();
builder.Services.AddScoped<EasyAlumni.Core.Interfaces.IPaymentService, EasyAlumni.Infrastructure.Services.ManualPaymentService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        
        // Apply migrations automatically if any pending
        context.Database.Migrate();

        // Seed data
        DbInitializer.SeedAsync(context, userManager, roleManager).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database migration/seeding.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
