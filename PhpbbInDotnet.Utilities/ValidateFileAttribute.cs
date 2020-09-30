using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace PhpbbInDotnet.Utilities
{
    public class ValidateFileAttribute : RequiredAttribute
    {
        public override bool IsValid(object value)
        {
            if (value == null)
            {
                return true;
            }
            if (value is IFormFile file)
            {
                try
                {
                    using var bmp = file.ToImage();
                    return bmp.Width <= 200 && bmp.Height <= 200;
                }
                catch { }
            }
            return false;
        }
    }
}
