using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.Forum;

WebApplication
    .CreateBuilder()
    .ConfigureServices()
    .Build()
    .ConfigureApplication()
    .Run();
