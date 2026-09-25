using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Logging;

/// <summary>
/// Log mọi HttpClient gọi ra ngoài (theo LoggingDelegatingHandler của VAS_CRM_BE).
/// Gắn: services.AddHttpClient&lt;X&gt;().AddHttpMessageHandler&lt;LoggingDelegatingHandler&gt;().
/// </summary>
public sealed class LoggingDelegatingHandler(ILogger<LoggingDelegatingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation("Outgoing {Method} {Url}", request.Method, request.RequestUri);
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                logger.LogInformation("Outgoing {Method} {Url} → {StatusCode} in {ElapsedMs} ms",
                    request.Method, request.RequestUri, (int)response.StatusCode, sw.ElapsedMilliseconds);
            else
                logger.LogWarning("Outgoing {Method} {Url} → non-success {StatusCode} in {ElapsedMs} ms",
                    request.Method, request.RequestUri, (int)response.StatusCode, sw.ElapsedMilliseconds);
            return response;
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
        {
            logger.LogCritical(ex, "Unable to connect to {Host}. Check the configured URL of the service", request.RequestUri?.Authority);
            return new HttpResponseMessage(HttpStatusCode.BadGateway) { RequestMessage = request };
        }
    }
}
