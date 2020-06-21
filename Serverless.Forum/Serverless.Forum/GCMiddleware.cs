using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Serverless.Forum
{
    public class GCMiddleware
    {
        private readonly RequestDelegate _next;

        public GCMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            await _next(httpContext);
            GC.Collect(2, GCCollectionMode.Forced, false);
            GC.WaitForPendingFinalizers();
        }
    }
}