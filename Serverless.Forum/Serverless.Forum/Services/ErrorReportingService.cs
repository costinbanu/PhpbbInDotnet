using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Serverless.Forum.Services
{
    public class ErrorReportingService
    {
        public Guid LogError(Exception ex, string path = null)
        {
            var id = Guid.NewGuid();
            File.AppendAllText(
                @"c:\users\costin\desktop\log.txt",
               Traverse(ex, id, path).AppendLine().ToString()
            );
            return id;
        }

        private StringBuilder Traverse(Exception ex, Guid id, string path, int level = 0, bool showTitle = true, StringBuilder current = null)
        {
            current ??= new StringBuilder();

            if (ex == null)
            {
                return current;
            }

            var offset = new string('\t', level);
            current.Append(ExtractText(ex, offset, id, showTitle, path));
            if (ex is AggregateException aex)
            {
                var uniqueExceptions = from e in aex.InnerExceptions
                                       group e by new { e.Message, e.StackTrace } into groups
                                       select new { Exception = groups.First(), Count = groups.Count() };

                foreach (var e in uniqueExceptions)
                {
                    current.Append(offset).AppendLine($"Displaying one unique inner exception (of {e.Count} non-unique reported):");
                    current.Append(ExtractText(e.Exception, offset, id, false, path));
                    current = Traverse(e.Exception.InnerException, id, path, level + 1, false, current);
                }
                return current;
            }
            else if (ex.InnerException != null)
            {
                current.Append(offset).AppendLine("--- Inner exception(s) ---");
                return Traverse(ex.InnerException, id, path, level + 1, false, current);
            }
            else 
            {
                return current.Append(ExtractText(ex, offset, id, showTitle, path));
            }
        }

        private string ExtractText(Exception ex, string offset, Guid id, bool showTitle, string path = null)
        {
            var sb = new StringBuilder();
            if (showTitle)
            {
                sb.Append(offset).AppendLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC - Exception while accessing '{path}':");
                sb.Append(offset).AppendLine($"ID: {id:n}");
            }
            sb.Append(offset).AppendLine(ex.Message);
            foreach (var line in ex.StackTrace?.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries) ?? Enumerable.Empty<string>())
            {
                sb.Append(offset).AppendLine(line);
            }
            return sb.ToString();
        }
    }
}
