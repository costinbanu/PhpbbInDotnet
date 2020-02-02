using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.IO;

namespace Serverless.Forum.Utilities
{
    public class ValidateFileAttribute : RequiredAttribute
    {
        public override bool IsValid(object value)
        {
            if (value is IFormFile file)
            {
                if (file == null)
                {
                    return false;
                }
                try
                {
                    using (var bmp = file.ToImage())
                    {
                        return bmp.Width <= 200 && bmp.Height <= 200;
                    }
                }
                catch { }
            }
            return false;
        }
    }
}
