namespace ServiceApp.Core.Common;

/// <summary>
/// Thrown by infrastructure adapters (Razorpay, SendGrid, ...) when the upstream
/// provider fails or returns an error. API hosts map it to 502 Bad Gateway.
/// </summary>
public class UpstreamServiceException : Exception
{
    public string Provider { get; }

    public UpstreamServiceException(string provider, string message, Exception? inner = null)
        : base(message, inner)
    {
        Provider = provider;
    }
}
