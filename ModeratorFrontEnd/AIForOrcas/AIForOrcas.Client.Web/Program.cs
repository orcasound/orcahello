var builder = WebApplication.CreateBuilder(args);

// Inject application settings
var appSettings = new AppSettings();
builder.Configuration.GetSection("AppSettings").Bind(appSettings);
builder.Services.AddSingleton<AppSettings>(appSettings);

builder.Services.AddSingleton<UserTagCache>();

// Inject external libraries (Extensions\External)
builder.ConfigureExternalUtilities(appSettings);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Inject authentication services and access policies (Extensions\Authentication)
builder.ConfigureAuthProviders();
builder.ConfigureModeratorPolicy(appSettings);

// Inject data services (Extensions\Services)
builder.ConfigureDataServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    // MapControllers in needed to enable authentication/authorization through Azure AD
    endpoints.MapControllers();
    endpoints.MapBlazorHub();
    endpoints.MapFallbackToPage("/_Host");
});

app.Run();