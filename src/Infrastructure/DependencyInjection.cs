using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Infrastructure.BackgroundJobs;
using CalendarManager.Infrastructure.CalendarProviders;
using CalendarManager.Infrastructure.Data;
using CalendarManager.Infrastructure.Data.Interceptors;
using CalendarManager.Infrastructure.Identity;
using CalendarManager.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{Services.Database}' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(connectionString);
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.EnrichSqlServerDbContext<ApplicationDbContext>();

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        builder.Services.AddAuthorizationBuilder();

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddApiEndpoints();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();

        builder.Services.AddDataProtection();
        builder.Services.AddMemoryCache();

        builder.Services.AddSingleton<IRefreshTokenProtector, DataProtectionRefreshTokenProtector>();
        builder.Services.AddSingleton<IOAuthStateStore, MemoryCacheOAuthStateStore>();
        builder.Services.AddScoped<ICalendarProviderClientFactory, CalendarProviderClientFactory>();

        builder.Services.Configure<GoogleCalendarOptions>(builder.Configuration.GetSection(GoogleCalendarOptions.SectionName));
        builder.Services.Configure<OutlookCalendarOptions>(builder.Configuration.GetSection(OutlookCalendarOptions.SectionName));

        builder.Services.AddHttpClient<GoogleCalendarClient>();
        builder.Services.AddScoped<ICalendarProviderClient>(sp => sp.GetRequiredService<GoogleCalendarClient>());

        builder.Services.AddHttpClient<OutlookCalendarClient>();
        builder.Services.AddScoped<ICalendarProviderClient>(sp => sp.GetRequiredService<OutlookCalendarClient>());

        builder.Services.AddQuartz(q =>
        {
            var jobKey = new JobKey(nameof(CalendarSyncJob));
            q.AddJob<CalendarSyncJob>(opts => opts.WithIdentity(jobKey));
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{nameof(CalendarSyncJob)}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(10).RepeatForever()));
        });
        builder.Services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);
    }
}
