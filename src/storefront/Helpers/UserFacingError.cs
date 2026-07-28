using Microsoft.Identity.Client;

namespace woodgrovedemo.Helpers
{
    /// <summary>
    /// Builds the error text that is safe to send to the browser.
    /// Raw exception and Microsoft Graph diagnostics can carry tenant IDs, client IDs, certificate
    /// subject names, authority URLs, internal host names, object IDs and correlation IDs, so they
    /// are tracked to Application Insights instead of being returned to the shopper.
    /// </summary>
    public static class UserFacingError
    {
        /// <summary>
        /// Shown when the shopper only needs to sign in again.
        /// </summary>
        public const string SignInRequired = "Your sign-in session has expired. Please sign in again.";

        /// <summary>
        /// The same prompt plus the marker that wwwroot/js/profile.js looks for when it decides to
        /// replace the profile panel with a sign-in button. "AcquireTokenSilent" is a public MSAL
        /// method name that is already hard-coded in the shipped client script, so echoing it back
        /// reveals nothing new about the server.
        /// </summary>
        public const string SignInRequiredForProfilePage = SignInRequired + " (AcquireTokenSilent)";

        /// <summary>
        /// Returns the sign-in prompt when the failure is a token problem the shopper can fix by
        /// signing in again; otherwise returns the caller's generic message.
        /// </summary>
        public static string For(Exception exception, string genericMessage)
        {
            return IsSignInRequired(exception) ? SignInRequired : genericMessage;
        }

        /// <summary>
        /// Same as <see cref="For"/>, for the responses that wwwroot/js/profile.js inspects for the
        /// sign-in marker.
        /// </summary>
        public static string ForProfilePage(Exception exception, string genericMessage)
        {
            return IsSignInRequired(exception) ? SignInRequiredForProfilePage : genericMessage;
        }

        private static bool IsSignInRequired(Exception? exception)
        {
            // Microsoft.Identity.Web wraps MsalUiRequiredException in
            // MicrosoftIdentityWebChallengeUserException, so walk the whole chain.
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is MsalUiRequiredException)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
