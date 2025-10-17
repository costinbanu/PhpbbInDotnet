using System;
using FastANPRDotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PhpbbInDotnet.Services.ImageProcessing;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddImageProcessingServices(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddSingleton<IImageSizeService, ImageSizeService>();
        services.AddFastAnpr(environment);
        services.AddSingleton<INumberPlateBlurringService, NumberPlateBlurringService>();
        return services;
    }
}
