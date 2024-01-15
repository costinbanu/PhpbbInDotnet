using Coravel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.RecurringTasks;
using System;

namespace Microsoft.AspNetCore.Builder
{
    public static class WebApplicationExtensions
    {
        public static WebApplication AddRecurringTasksScheduler(this WebApplication app)
        {
            const string key = "RecurringTasksTimeToRun";
            var timeToRun = app.Configuration.GetValue<string>(key);
            int hour, minute;
            try
            {
                var parts = timeToRun.Split(':');
                hour = int.Parse(parts[0]);
                minute = int.Parse(parts[1]);
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid configuration value '{timeToRun}' for '{key}'. Check the app settings!'", ex);
            }

            app.Services.UseScheduler(scheduler =>
            {
                var config = scheduler.Schedule<Orchestrator>().DailyAt(hour, minute);
                //if (app.Environment.IsDevelopment())
                //{
                //    config.RunOnceAtStart();
                //}
            });

            return app;
        }
    }
}
