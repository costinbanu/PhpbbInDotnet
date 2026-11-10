using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Objects.BotDetectorDtos;
using PhpbbInDotnet.Objects.Configuration;

namespace PhpbbInDotnet.Services;

class BotDetectionService : IBotDetectionService
{
    private readonly BotDetectorOptions _botDetectorOptions;
    private readonly HttpClient _botDetectorApiClient;

    public BotDetectionService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _botDetectorOptions = configuration.GetObject<BotDetectorOptions>();
        _botDetectorApiClient = httpClientFactory.CreateClient(_botDetectorOptions.ClientName!);
    }

    public async Task<bool> IsBot(string? botDetectorResponse, string remoteIp)
    {
        var response = await _botDetectorApiClient.PostAsync(
            requestUri: _botDetectorOptions.RelativeUri,
            content: JsonContent.Create(new SiteVerifyRequest
            {
                Secret = _botDetectorOptions.SecretKey,
                Response = botDetectorResponse,
                RemoteIp = remoteIp
            }));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>();

        if (!result!.Success && result.ErrorCodes.Contains("invalid-input-response") || result.ErrorCodes.Contains("missing-input-response"))
        {
            return true;
        }
        else if (!result.Success)
        {
            throw new InvalidOperationException($"Bot detection failed with error codes: {string.Concat(",", result.ErrorCodes)}");
        }

        return false;
    }
}