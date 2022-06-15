﻿using PhpbbInDotnet.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IAdminForumService, AdminForumService>();
            services.AddScoped<IAdminUserService, AdminUserService>();
            services.AddScoped<IWritingToolsService, WritingToolsService>();
            services.AddScoped<IForumTreeService, ForumTreeService>();
            services.AddScoped<IPostService, PostService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IStorageService, StorageService>();
            services.AddScoped<IModeratorService, ModeratorService>();
            services.AddScoped<IBBCodeRenderingService, BBCodeRenderingService>();
            services.AddScoped<IStatisticsService, StatisticsService>();
            services.AddScoped<IOperationLogService, OperationLogService>();
            services.AddSingleton<ITimeService, TimeService>();
            services.AddSingleton<IFileInfoService, FileInfoService>();

            services.AddHostedService<CleanupService>();

            return services;
        }
    }
}