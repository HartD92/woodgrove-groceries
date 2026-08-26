using Microsoft.IdentityModel.Protocols.OpenIdConnect;

public static class AuthRedirectCustomizer
{
    private const string StepUpClaimsValue = "%7B%22access_token%22%3A%7B%22acrs%22%3A%7B%22essential%22%3Atrue%2C%22value%22%3A%22c1%22%7D%7D%7D";

    public static void Apply(OpenIdConnectMessage protocolMessage, IDictionary<string, string?> items, string? defaultDomain = null)
    {
        ArgumentNullException.ThrowIfNull(protocolMessage);
        ArgumentNullException.ThrowIfNull(items);

        if (TryGetValue(items, "force", out _))
        {
            protocolMessage.Prompt = "login";
        }

        if (TryGetValue(items, "StepUp", out _))
        {
            protocolMessage.Parameters["claims"] = StepUpClaimsValue;
        }

        if (TryGetValue(items, "prompt", out var prompt))
        {
            protocolMessage.Prompt = prompt;
        }

        if (TryGetValue(items, "ui_locales", out var uiLocales))
        {
            protocolMessage.Parameters["mkt"] = uiLocales;
            protocolMessage.UiLocales = uiLocales;
        }

        if (TryGetValue(items, "login_hint", out var loginHint))
        {
            protocolMessage.LoginHint = loginHint;
        }

        if (TryGetValue(items, "domain_hint", out var domainHint))
        {
            protocolMessage.DomainHint = domainHint;
        }

        if (TryGetValue(items, "query-string", out var queryString))
        {
            foreach (var parameter in ParseQueryString(queryString))
            {
                protocolMessage.Parameters[parameter.Key] = parameter.Value;
            }
        }

        var requestedDomain = ResolveDomain(items, defaultDomain);
        if (!string.IsNullOrWhiteSpace(requestedDomain) && !string.IsNullOrWhiteSpace(protocolMessage.IssuerAddress))
        {
            var issuerAddress = new UriBuilder(protocolMessage.IssuerAddress)
            {
                Host = requestedDomain
            };

            protocolMessage.IssuerAddress = issuerAddress.Uri.ToString();
        }
    }

    private static string? ResolveDomain(IDictionary<string, string?> items, string? defaultDomain)
    {
        if (TryGetValue(items, "domain", out var domain))
        {
            return domain;
        }

        return string.IsNullOrWhiteSpace(defaultDomain) ? null : defaultDomain;
    }

    private static bool TryGetValue(IDictionary<string, string?> items, string key, out string value)
    {
        if (items.TryGetValue(key, out var itemValue) && !string.IsNullOrWhiteSpace(itemValue))
        {
            value = itemValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQueryString(string queryString)
    {
        foreach (var rawParameter in queryString.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = rawParameter.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(rawParameter[..separatorIndex]);
            var value = Uri.UnescapeDataString(rawParameter[(separatorIndex + 1)..]);
            yield return new KeyValuePair<string, string>(key, value);
        }
    }
}
