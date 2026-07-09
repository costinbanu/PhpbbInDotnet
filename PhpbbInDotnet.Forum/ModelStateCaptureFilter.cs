using Microsoft.AspNetCore.Mvc.Filters;

public class ModelStateCaptureFilter : IPageFilter
{
    public void OnPageHandlerSelected(PageHandlerSelectedContext context)
    {
        context.HttpContext.Items["ModelState"] = context.ModelState;
    }

    public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        
    }

    public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
    {
        
    }
}