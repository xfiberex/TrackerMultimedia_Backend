using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Domain.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class HttpOrHttpsUrlAttribute : ValidationAttribute
{
    public HttpOrHttpsUrlAttribute()
        : base("The {0} field must be a valid HTTP or HTTPS URL.") { }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string url)
        {
            return false;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
