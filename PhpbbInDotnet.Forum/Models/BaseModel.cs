using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Models
{
    public abstract class BaseModel : PageModel
    {
        protected IActionResult PageWithError(string errorKey, string errorMessage)
            => PageWithErrorImpl(errorKey, errorMessage);

        protected IActionResult PageWithError(string errorKey, string errorMessage, Action toDoBeforeReturn)
            => PageWithErrorImpl(errorKey, errorMessage, toDoBeforeReturn);

        protected IActionResult PageWithError(string errorKey, string errorMessage, Func<IActionResult> resultFactory)
            => PageWithErrorImpl(errorKey, errorMessage, resultFactory: resultFactory);

        protected IActionResult PageWithError(string errorKey, string errorMessage, Action toDoBeforeReturn, Func<IActionResult> resultFactory)
            => PageWithErrorImpl(errorKey, errorMessage, toDoBeforeReturn, resultFactory);

        protected Task<IActionResult> PageWithErrorAsync(string errorKey, string errorMessage)
            => PageWithErrorImplAsync(errorKey, errorMessage);

        protected Task<IActionResult> PageWithErrorAsync(string errorKey, string errorMessage, Action toDoBeforeReturn)
            => PageWithErrorImplAsync(errorKey, errorMessage, toDoBeforeReturn);

        protected Task<IActionResult> PageWithErrorAsync(string errorKey, string errorMessage, Func<Task<IActionResult>> resultFactory)
            => PageWithErrorImplAsync(errorKey, errorMessage, resultFactory: resultFactory);

        protected Task<IActionResult> PageWithErrorAsync(string errorKey, string errorMessage, Action toDoBeforeReturn, Func<Task<IActionResult>> resultFactory)
            => PageWithErrorImplAsync(errorKey, errorMessage, toDoBeforeReturn, resultFactory);

        private IActionResult PageWithErrorImpl(string errorKey, string errorMessage, Action? toDoBeforeReturn = null, Func<IActionResult>? resultFactory = null)
        {
            toDoBeforeReturn?.Invoke();
            ModelState.AddModelError(errorKey, errorMessage);
            return resultFactory is not null ? resultFactory() : Page();
        }

        private async Task<IActionResult> PageWithErrorImplAsync(string errorKey, string errorMessage, Action? toDoBeforeReturn = null, Func<Task<IActionResult>>? resultFactory = null)
        {
            toDoBeforeReturn?.Invoke();
            ModelState.AddModelError(errorKey, errorMessage);
            return resultFactory is not null ? await resultFactory() : Page();
        }
    }
}
