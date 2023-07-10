using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Services.Locks;
using PhpbbInDotnet.Services.Storage;
using System;
using StorageOptions = PhpbbInDotnet.Objects.Configuration.Storage;

namespace Microsoft.Extensions.DependencyInjection
{
	public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IAdminForumService, AdminForumService>();
            services.AddScoped<IAdminUserService, AdminUserService>();
            services.AddScoped<IWritingToolsService, WritingToolsService>();
            services.AddScoped<IForumTreeService, ForumTreeService>();
            services.AddScoped<IPostService, PostService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IModeratorService, ModeratorService>();
            services.AddScoped<IBBCodeRenderingService, BBCodeRenderingService>();
            services.AddScoped<IStatisticsService, StatisticsService>();
            services.AddScoped<IOperationLogService, OperationLogService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IRazorViewService, RazorViewService>();
            services.AddScoped<IUserProfileDataValidationService, UserProfileDataValidationService>();

            var storageOptions = configuration.GetObject<StorageOptions>();
            switch (storageOptions.StorageType)
            {
                case StorageType.HardDisk:
                    services.AddScoped<IStorageService, DiskStorageService>();
                    services.AddScoped<ILockingService, InMemoryLockingService>();
                    break;

                case StorageType.AzureStorage:
                    if (string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
                    {
                        throw new ArgumentException($"Selected StorageType is AzureStorage but the {nameof(StorageOptions.ConnectionString)} property is null or empty.");
                    }
					if (string.IsNullOrWhiteSpace(storageOptions.ContainerName))
					{
						throw new ArgumentException($"Selected StorageType is AzureStorage but the {nameof(StorageOptions.ContainerName)} property is null or empty.");
					}
					services.AddAzureClients(builder => builder.AddBlobServiceClient(storageOptions.ConnectionString));
                    services.AddSingleton(sp => sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(storageOptions.ContainerName));
                    services.AddScoped<IStorageService, AzureStorageService>();
                    services.AddScoped<ILockingService, AzureLockingService>();
                    break;

                default:
                    throw new InvalidOperationException("Unknown StorageType in configuration.");
            }


			services.AddSingleton<ITimeService, TimeService>();
            services.AddSingleton<IEncryptionService, EncryptionService>();
            services.AddSingleton<IAnonymousSessionCounter, AnonymousSessionCounter>();
            services.AddSingleton<IImageResizeService, ImageResizeService>();

            return services;
        }
    }
}
