using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetSuite;
using System.Data.Odbc;
using TPL_TM.Data;
using TPL_TM.Services.AI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(provider =>
{
    var config = builder.Configuration.GetSection("NetSuiteConfig");
    return new NetSuiteClient(
        config["ConsumerKey"],
        config["ConsumerSecret"],
        config["AccessToken"],
        config["TokenSecret"],
        config["Realm"],
        config["BaseUrl"]
    );
});


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services.AddScoped<IKnowledgeService, KnowledgeService>();

builder.Services.AddScoped<IOpenAIService, BedrockService>();

//builder.Services.AddHttpClient<IOpenAIService, OpenAIService>();

builder.Services.AddScoped<ManufacturingAIService>();

// Register the NetSuiteClient service
builder.Services.AddHttpClient();

builder.Services.AddRazorPages();

builder.Services.AddControllers(); // Enables API controller support

var app = builder.Build();

// Configure the HTTP request pipeline.
async Task SeedRolesAsync(IServiceProvider serviceProvider)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = { "Admin", "Supervisor", "User", "Management" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
using (var scope = app.Services.CreateScope())
{
    await SeedRolesAsync(scope.ServiceProvider);
}
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers(); // Maps the [ApiController] routes
// Seed roles and admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedData.Initialize(services);
}

app.Run();
