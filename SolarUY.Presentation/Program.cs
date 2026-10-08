using SolarUY.Presentation.Components;
using FluentValidation;
using Radzen;
using SolarUY.Application.ParquesSolares;
using SolarUY.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSolarInfrastructure(builder.Configuration);
builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
builder.Services.AddScoped<IValidator<CrearParqueSolarDto>, CrearParqueSolarDtoValidator>();
builder.Services.AddRadzenComponents();
// OpenID debe agregar su esquema externo en un requerimiento separado.
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
