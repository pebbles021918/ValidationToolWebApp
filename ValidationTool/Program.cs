using ValidationTool.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.CookiePolicy;

var builder = WebApplication.CreateBuilder(args);

// Register HttpClient for OpenAI API
builder.Services.AddHttpClient<ChatGptService>();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddLogging();

// Configure secure cookie policy
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.None;
    options.Secure = CookieSecurePolicy.Always; // Only send cookies over HTTPS
    options.HttpOnly = HttpOnlyPolicy.Always; 
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts(); 
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseCookiePolicy(); // Add secure cookie middleware

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
