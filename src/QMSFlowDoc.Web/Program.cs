using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Domain.Identity;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Web.Components;
using QMSFlowDoc.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
var connectionStringRaw = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
var connectionString = ResolvePortableConnectionString(connectionStringRaw);

await PendingRestoreService.ApplyPendingRestoreAsync(builder.Configuration);

builder.Services.AddDbContext<QmsDbContext>(options =>
    options.UseSqlite(connectionString, b => b.MigrationsAssembly("QMSFlowDoc.Infrastructure")));

// Identity Configuration
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options => {
    // ISO 15189 §4.2 - Política de contraseñas robusta
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // ISO 15189 §7.6 - Bloqueo por intentos fallidos
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<QmsDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, QMSFlowDoc.Web.Security.MigrationPasswordHasher>();

// Document Storage and Document Management Services
var documentStorageConfig = builder.Configuration.GetSection("DocumentStorage");
var rootPathConfig = documentStorageConfig["RootPath"] ?? throw new InvalidOperationException("DocumentStorage:RootPath not configured.");
var rootPath = ResolvePortablePath(rootPathConfig);
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(rootPath, "DataProtection-Keys");

try
{
    Directory.CreateDirectory(dataProtectionKeysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"⚠ Warning: Unable to create or use DataProtection directory '{dataProtectionKeysPath}': {ex.Message}. Falling back to Ephemeral Data Protection.");
    builder.Services.AddDataProtection();
}

builder.Services.AddSingleton<QMSFlowDoc.DocumentStorage.IDocumentStorageService>(sp =>
    new QMSFlowDoc.DocumentStorage.CentralDocumentStorageService(rootPath, sp.GetRequiredService<ILogger<QMSFlowDoc.DocumentStorage.CentralDocumentStorageService>>()));

builder.Services.AddSingleton<QMSFlowDoc.Application.Services.Documents.IPdfWatermarkService, QMSFlowDoc.Infrastructure.Services.Documents.PdfWatermarkService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Folders.IFolderService, QMSFlowDoc.Infrastructure.Services.Folders.FolderService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Documents.IDocumentService, QMSFlowDoc.Infrastructure.Services.Documents.DocumentService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Inventory.IInventoryService, QMSFlowDoc.Infrastructure.Services.Inventory.InventoryService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Staff.IStaffService, QMSFlowDoc.Infrastructure.Services.Staff.StaffService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Identity.IUserService, QMSFlowDoc.Infrastructure.Services.Identity.UserService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Identity.IPermissionService, QMSFlowDoc.Infrastructure.Services.Identity.PermissionService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Quality.IQualityService, QMSFlowDoc.Infrastructure.Services.Quality.QualityService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Equipment.IEquipmentService, QMSFlowDoc.Infrastructure.Services.Equipment.EquipmentService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.EQA.IEQAService, QMSFlowDoc.Infrastructure.Services.EQA.EQAService>();

builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/login";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllersWithViews();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await QMSFlowDoc.Infrastructure.Seed.DbInitializer.SeedIdentityAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "An error occurred while migrating or seeding the database.");
        throw;
    }
}

await app.RunAsync();

#pragma warning disable CS8321 // Local function is declared but never used
string ResolvePortableConnectionString(string connStr)
{
    try
    {
        var connBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connStr);
        if (!string.IsNullOrWhiteSpace(connBuilder.DataSource) && connBuilder.DataSource != ":memory:")
        {
            connBuilder.DataSource = ResolvePortablePath(connBuilder.DataSource);
            return connBuilder.ToString();
        }
    }
    catch
    {
        // Fallback to original
    }
    return connStr;
}

string ResolvePortablePath(string configuredPath)
{
    if (string.IsNullOrWhiteSpace(configuredPath)) return configuredPath;
    
    if (configuredPath.Contains(@"C:\Users\SERVIDOR", StringComparison.OrdinalIgnoreCase))
    {
        var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var subPath = configuredPath.Replace(@"C:\Users\SERVIDOR\Documents\", "", StringComparison.OrdinalIgnoreCase)
                                     .Replace(@"C:\Users\SERVIDOR\", "", StringComparison.OrdinalIgnoreCase);
        return Path.Combine(myDocuments, "QMS", subPath);
    }
    return configuredPath;
}
#pragma warning restore CS8321
