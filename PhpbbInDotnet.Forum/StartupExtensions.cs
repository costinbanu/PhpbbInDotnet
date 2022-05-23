using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using Serilog;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhpbbInDotnet.Forum
{
    public static class StartupExtensions
    {
        public static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder webApplicationBuilder)
        {
            var services = webApplicationBuilder.Services;
            var config = webApplicationBuilder.Configuration;
            var environment = webApplicationBuilder.Environment;

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });


            services.AddDistributedMemoryCache();

            services.AddSession(options =>
            {
                options.IdleTimeout = config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies 
                // is needed for a given request.
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();

            var builder = services.AddMvc(o => o.EnableEndpointRouting = false)
                .AddRazorOptions(o =>
                {
                    o.PageViewLocationFormats.Add("~/Pages/CustomPartials/{0}.cshtml");
                    o.PageViewLocationFormats.Add("~/Pages/CustomPartials/Admin/{0}.cshtml");
                    o.PageViewLocationFormats.Add("~/Pages/CustomPartials/Email/{0}.cshtml");
                })
                .AddJsonOptions(o =>
                {
                    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    o.JsonSerializerOptions.Converters.Add(new DateTimeConverter());
                    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });

            services.Configure<IISServerOptions>(options =>
            {
                options.AutomaticAuthentication = false;
            });

            services.AddDataProtection();
            services.AddAntiforgery(o => o.HeaderName = "XSRF-TOKEN");

            services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = 1073741824;
            });
            if (environment.IsDevelopment())
            {
                services.Configure<KestrelServerOptions>(options =>
                {
                    options.Limits.MaxRequestBodySize = 1073741824; // if not set default value is: 30 MB
                });
            }
            else
            {
                services.Configure<FormOptions>(x =>
                {
                    x.ValueLengthLimit = 1073741824;
                    x.MultipartBodyLengthLimit = 1073741824; // if not set default value is: 128 MB
                    x.MultipartHeadersLengthLimit = 1073741824;
                });
            }

            services.AddSingleton<CommonUtils>();
            services.AddSingleton<AnonymousSessionCounter>();
            services.AddSingleton<FileExtensionContentTypeProvider>();

            services.AddScoped<AdminForumService>();
            services.AddScoped<AdminUserService>();
            services.AddScoped<WritingToolsService>();
            services.AddScoped<ForumTreeService>();
            services.AddScoped<PostService>();
            services.AddScoped<UserService>();
            services.AddScoped<StorageService>();
            services.AddScoped<ModeratorService>();
            services.AddScoped<BBCodeRenderingService>();
            services.AddScoped<StatisticsService>();
            services.AddScoped<OperationLogService>();
            services.AddScoped<AuthenticationMiddleware>();

            services.AddTransient<IHttpContextAccessor, HttpContextAccessor>();
            services.AddTransient<LanguageProvider>();

            var recaptchaOptions = config.GetObject<Recaptcha>();
            services.AddHttpClient(recaptchaOptions.ClientName, client => client.BaseAddress = new Uri(recaptchaOptions.BaseAddress!));

            var imageProcessorOptions = config.GetObject<ExternalImageProcessor>();
            if (imageProcessorOptions.Api?.Enabled == true)
            {
                services.AddHttpClient(imageProcessorOptions.Api.ClientName, client =>
                {
                    client.BaseAddress = new Uri(imageProcessorOptions.Api.BaseAddress!);
                    client.DefaultRequestHeaders.Add("X-API-Key", imageProcessorOptions.Api.ApiKey);
                });
            }

            services.AddForumDbContext(config);

            services.AddLazyCache();

            services.AddHostedService<CleanupService>();
            services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromMinutes(5));

            DefaultTypeMap.MatchNamesWithUnderscores = true;
            ServicePointManager.DefaultConnectionLimit = 10;

            webApplicationBuilder.Host
                .UseSerilog((context, config) =>
                {
                    var format = "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}";
                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        config.WriteTo.Console(outputTemplate: format);
                    }
                    else
                    {
                        config.WriteTo.File(
                            path: Path.Combine("logs", "log.txt"),
                            restrictedToMinimumLevel: LogEventLevel.Warning,
                            rollingInterval: RollingInterval.Day,
                            outputTemplate: format
                        );
                    }
                });


            return webApplicationBuilder;
        }

        public static WebApplication ConfigureApplication (this WebApplication webApplication)
        {
            if (webApplication.Environment.IsDevelopment())
            {
                webApplication.UseDeveloperExceptionPage();
            }
            else
            {
                webApplication.UseExceptionHandler(errorApp =>
                {
                    errorApp.Run(async context =>
                    {
                        var handler = context.Features.Get<IExceptionHandlerPathFeature>();
                        if (handler is not null)
                        {
                            var utils = context.RequestServices.GetService<CommonUtils>();
                            var userService = context.RequestServices.GetService<UserService>();
                            var user = userService?.ClaimsPrincipalToAuthenticatedUser(context.User);
                            var path = context.Request?.Path.Value ?? handler.Path;
                            var id = utils?.HandleError(handler.Error, $"URL: {path}{context.Request?.QueryString}. UserId: {user?.UserId.ToString() ?? "N/A"}. UserName: {user?.Username ?? "N/A"}");

                            if (path?.Equals("/Error", StringComparison.InvariantCultureIgnoreCase) != true)
                            {
                                context.Response.Redirect($"/Error?errorId={id}");
                            }
                            else
                            {
                                await context.Response.WriteAsync($"An error occurred. ID: {id}");
                            }
                        }
                    });
                });
                webApplication.UseHsts();
            }

            webApplication.UseRouting();
            webApplication.UseRequestLocalization();
            webApplication.UseHttpsRedirection();
            webApplication.UseStaticFiles();
            webApplication.UseCookiePolicy();
            webApplication.UseAuthentication();
            webApplication.UseSession();
            webApplication.UseMiddleware<AuthenticationMiddleware>();
            webApplication.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
            });

            return webApplication;
        }

        class DateTimeConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                Debug.Assert(typeToConvert == typeof(DateTime));
                return DateTime.Parse(reader.GetString()!);
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssZ"));
            }
        }
    }
}
