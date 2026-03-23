using System.Net;

namespace Conspectare.Client;

public class ConspectareApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }

    public ConspectareApiException(HttpStatusCode statusCode, string? responseBody, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public ConspectareApiException(HttpStatusCode statusCode, string? responseBody, string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
