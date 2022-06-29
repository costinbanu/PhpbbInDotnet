using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IEmailService
    {
        Task SendEmail(string to, string subject, string bodyRazorViewName, object bodyRazorViewModel);
    }
}