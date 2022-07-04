using BlazorChat.Server.Hubs;
using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();


EnvironmentVarKeys.CheckEnvironment(out bool enableBlob, out bool enableTranslation);

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

builder.Services.AddSingleton<IIdGeneratorService, RandomIdGeneratorService>();
builder.Services.AddSingleton<ILoginDataService, LoginDataService>();
builder.Services.AddSingleton<IUserDataService, UserDataService>();
builder.Services.AddSingleton<IChannelDataService, ChannelDataService>();
builder.Services.AddSingleton<IMessageDataService, MessageDataService>();
builder.Services.AddSingleton<IFormDataService, FormDataService>();
builder.Services.AddSingleton<ICallSupportService, CallSupportService>();
builder.Services.AddSingleton<IDatabaseConnection, CosmosDatabaseConnection>();
builder.Services.AddSingleton<IHubManager, HubManager>();

#if ENABLE_ADMINAPI_AUTH
builder.Services.AddSingleton<IAdminAuthService, BearerCompareAdminAuthService>();
#else
builder.Services.AddSingleton<IAdminAuthService, PlaceholderAuthAdminAuthService>();
#endif

if (enableBlob)
{
    builder.Services.AddTransient<IStorageService, AzureBlobStorageService>();
}
if (enableTranslation)
{
    builder.Services.AddSingleton<ITranslationService, AzureTranslatorService>();
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
builder.Logging.AddConsole();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseWebAssemblyDebugging();
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
