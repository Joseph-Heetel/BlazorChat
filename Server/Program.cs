using CustomBlazorApp.Server.Hubs;
using CustomBlazorApp.Server.Services;
using CustomBlazorApp.Shared;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using CustomBlazorApp.Server.Services.DatabaseWrapper;
using CustomBlazorApp.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
// Add services to the container.

EnvironmentVarKeys.CheckEnvironment(out bool enableBlob);

builder.Services.AddControllersWithViews().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new ItemIdConverter());
    options.JsonSerializerOptions.Converters.Add(new ByteArrayConverter());
});
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSignalRCore();
builder.Services.AddCors();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<IHashService, SHAHashService>();
builder.Services.AddSingleton<IIdGeneratorService, RandomIdGeneratorService>();
builder.Services.AddTransient<ILoginDataService, LoginDataService>();
builder.Services.AddTransient<IUserDataService, UserDataService>();
builder.Services.AddTransient<IChannelDataService, ChannelDataService>();
builder.Services.AddTransient<IMessageDataService, MessageDataService>();
builder.Services.AddTransient<IFormDataService, FormDataService>();
builder.Services.AddSingleton<ICallSupportService, CallSupportService>();
builder.Services.AddSingleton<IDatabaseConnection, CosmosDatabaseConnection>();

#if ENABLE_ADMINAPI_AUTH
builder.Services.AddSingleton<IAdminAuthService, BearerCompareAdminAuthService>();
#else
builder.Services.AddSingleton<IAdminAuthService, NoAuthAdminAuthService>();
#endif

if (enableBlob)
{
    builder.Services.AddTransient<IStorageService, AzureBlobStorageService>();
}

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;

}).AddCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Logging.AddAzureWebAppDiagnostics();

var app = builder.Build();

app.UseDeveloperExceptionPage();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseHttpLogging();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapHub<ChatHub>("hubs/chat");
app.MapControllers();
app.MapFallbackToFile("index.html");

Console.Write("Setting up DB ... ");
var dbconnection = app.Services.GetRequiredService<IDatabaseConnection>() as CosmosDatabaseConnection;
await dbconnection!.Init();
Console.WriteLine("Done");

app.Run();
