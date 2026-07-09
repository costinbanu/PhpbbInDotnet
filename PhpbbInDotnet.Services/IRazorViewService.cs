using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IRazorViewService
    {
        Task<string> RenderRazorViewToString(string viewPath, object model);
    }
}