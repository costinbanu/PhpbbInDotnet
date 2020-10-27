using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaulMiami.AspNetCore.Mvc.Recaptcha;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhpbbInDotnet.Forum
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostEnvironment env)
        {
            Configuration = configuration;
            Env = env;
        }

        public IConfiguration Configuration { get; private set; }
        public IHostEnvironment Env { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddSingleton(Configuration);

            services.AddDistributedMemoryCache();

            services.AddSession(options => {
                options.IdleTimeout = TimeSpan.FromMinutes(Configuration.GetValue<double>("UserActivityTrackingIntervalMinutes"));
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
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
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
                    o.JsonSerializerOptions.IgnoreNullValues = true;
                });

#if DEBUG
            if (Env.IsDevelopment())
            {
                builder.AddRazorRuntimeCompilation();
            }
#endif
            services.Configure<IISServerOptions>(options =>
            {
                options.AutomaticAuthentication = false;
            });

            services.AddDataProtection();
            services.AddAntiforgery(o => o.HeaderName = "XSRF-TOKEN");

            services.AddRecaptcha(new RecaptchaOptions
            {
                SiteKey = Configuration["Recaptcha:SiteKey"],
                SecretKey = Configuration["Recaptcha:SecretKey"],
            });

            services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = 1073741824;
            });
            if (Env.IsDevelopment())
            {
                services.Configure<KestrelServerOptions>(options =>
                {
                    options.Limits.MaxRequestBodySize = 1073741824; // if don't set default value is: 30 MB
                });
            }
            else
            {
                services.Configure<FormOptions>(x =>
                {
                    x.ValueLengthLimit = 1073741824;
                    x.MultipartBodyLengthLimit = 1073741824; // if don't set default value is: 128 MB
                x.MultipartHeadersLengthLimit = 1073741824;
                });
            }
            services.AddHttpClient();

            services.AddSingleton<CommonUtils>();
            services.AddSingleton<AnonymousSessionCounter>();
            services.AddSingleton<BBTagFactory>();
            services.AddScoped<AdminForumService>();
            services.AddScoped<AdminUserService>();
            services.AddScoped<WritingToolsService>();
            services.AddScoped<CacheService>();
            services.AddScoped<ForumTreeService>();
            services.AddScoped<PostService>();
            services.AddScoped<UserService>();
            services.AddScoped<StorageService>();
            services.AddScoped<ModeratorService>();
            services.AddScoped<BBCodeRenderingService>();
            services.AddDbContext<ForumDbContext>(options => options.UseMySQL(Configuration["ForumDbConnectionString"], o => o.CommandTimeout(60)), ServiceLifetime.Scoped);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json",
                             optional: false,
                             reloadOnChange: true)
                .AddEnvironmentVariables();

            var utils = app.ApplicationServices.GetService<CommonUtils>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                builder.AddUserSecrets<Startup>();
            }
            else
            {
                app.UseExceptionHandler(errorApp =>
                {
                    errorApp.Run(async context =>
                    {
                        var handler = context.Features.Get<IExceptionHandlerPathFeature>();
                        if ((context.Request?.Path.HasValue ?? false) && !context.Request.Path.Value.Equals("/Error", StringComparison.InvariantCultureIgnoreCase))
                        {
                            context.Response.Redirect($"/Error?errorId={utils.HandleError(handler.Error, $"Path: {handler.Path}")}");
                        }
                        else
                        {
                            var user = await utils.DecompressObject<LoggedUser>(Convert.FromBase64String(context.User?.Claims?.FirstOrDefault()?.Value ?? string.Empty));
                            var id = utils.HandleError(handler.Error, $"Path: {handler.Path} ({context.Request.Path}{context.Request.QueryString}). User: {user}.");
                            await context.Response.WriteAsync($"A intervenit o eroare. ID: {id}");
                        }
                    });
                });
                app.UseHsts();
            }

            app.UseRouting();
            app.UseRequestLocalization();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseSession();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
            });

            Configuration = builder.Build();
        }
    }

    public class DateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Debug.Assert(typeToConvert == typeof(DateTime));
            return DateTime.Parse(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssZ"));
        }
    }
}