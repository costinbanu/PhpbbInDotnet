using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Services
{
    public interface IAnonymousSessionCounter
    {
        int GetActiveBotCount();
        int GetActiveSessionCount();
        IEnumerable<BotData> GetBots();
        void UpsertBot(string ip, string userAgent, TimeSpan expiration);
        void UpsertSession(string sessionId, TimeSpan expiration);
    }
}