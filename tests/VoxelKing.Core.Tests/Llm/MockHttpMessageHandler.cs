namespace VoxelKing.Core.Tests.Llm;

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Mock HTTP message handler for simulating Gemini REST API responses without network calls.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public HttpStatusCode StatusCodeToReturn { get; set; } = HttpStatusCode.OK;
    public string ResponseBodyToReturn { get; set; } = "{}";
    public Exception? ExceptionToThrow { get; set; }
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public MockHttpMessageHandler() { }

    public MockHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
    {
        StatusCodeToReturn = statusCode;
        ResponseBodyToReturn = responseBody;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content != null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        var response = new HttpResponseMessage(StatusCodeToReturn)
        {
            Content = new StringContent(ResponseBodyToReturn, Encoding.UTF8, "application/json")
        };

        return response;
    }
}
