using PhpbbInDotnet.Objects.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.BackgroundProcessing.Handlers
{
    internal class AddPostCommandHandler(ILogger logger) : IMessageHandler<AddPostCommand>
    {

        public Task Handle(AddPostCommand message, CancellationToken cancellationToken)
        {
            logger.Information("AddPostCommand {cmd}", message);
            return Task.CompletedTask;
        }
    }
}
