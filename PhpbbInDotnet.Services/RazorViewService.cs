using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class RazorViewService : IRazorViewService
    {
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IHttpContextAccessor _actionContextAccessor;

        public RazorViewService(ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider, IHttpContextAccessor actionContextAccessor)
        {
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _actionContextAccessor = actionContextAccessor;
        }

        public async Task<string> RenderRazorViewToString(string viewPath, object model)
        {
            var httpContext = _actionContextAccessor.HttpContext ?? throw new Exception($"Error rendering view '{viewPath}': Expected a HttpContext but found none");
            var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), new PageActionDescriptor());
            var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: false);

            var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };

            using var sw = new StringWriter();
            var viewContext = new ViewContext(
                actionContext,
                viewResult.View ?? throw new Exception($"{viewPath} does not match any available view"),
                viewDictionary,
                new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                sw,
                new HtmlHelperOptions())
            {
                RouteData = actionContext.HttpContext.GetRouteData()
            };

            await viewResult.View.RenderAsync(viewContext);
            return sw.GetStringBuilder().ToString();
        }
    }
}
