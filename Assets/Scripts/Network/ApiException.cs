using System;

public sealed class ApiException : Exception
{
    public long StatusCode { get; }
    public string ResponseBody { get; }

    public ApiException(string message, long statusCode = 0, string responseBody = null, Exception inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
