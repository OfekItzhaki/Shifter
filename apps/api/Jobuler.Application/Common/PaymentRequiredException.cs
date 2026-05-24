namespace Jobuler.Application.Common;

/// <summary>
/// Thrown when a group's subscription has expired and the requested operation
/// requires an active subscription. Maps to HTTP 402 Payment Required.
/// </summary>
public class PaymentRequiredException : Exception
{
    public PaymentRequiredException(string message) : base(message) { }
}
