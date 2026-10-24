using System.Net;

namespace FocusBar.Core.Sources.School;

/// <summary>The school website returned an error or something FocusBar couldn't read.</summary>
public class SchoolApiException : Exception
{
    public SchoolApiException(string message, HttpStatusCode? status = null, Exception? inner = null)
        : base(message, inner)
    {
        Status = status;
    }

    public HttpStatusCode? Status { get; }
}

/// <summary>The saved sign-in is missing or expired; the student needs to sign in again.</summary>
public sealed class SchoolSignInException : SchoolApiException
{
    public SchoolSignInException(string message, HttpStatusCode? status = null)
        : base(message, status)
    {
    }
}
