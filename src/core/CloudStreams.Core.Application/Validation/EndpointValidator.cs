// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Text.RegularExpressions;

namespace CloudStreams.Core.Application.Validation;

/// <summary>
/// Validates <see cref="Endpoint"/>s, and the authentication policies they use (see #218)
/// </summary>
public static partial class EndpointValidator
{

    /// <summary>
    /// Validates the specified endpoint
    /// </summary>
    /// <param name="endpoint">The endpoint to validate</param>
    /// <param name="consume">A boolean indicating whether cloud events are consumed from the endpoint, rather than delivered to it</param>
    /// <param name="path">The path of the endpoint in the resource, used to key errors</param>
    /// <returns>The errors, keyed by the path of the property they concern, if any</returns>
    public static IReadOnlyList<(string Path, string Error)> Validate(Endpoint? endpoint, bool consume, string path)
    {
        var errors = new List<(string, string)>();
        if (endpoint?.Uri == null)
        {
            errors.Add((path, $"'{path}' must be set"));
            return errors;
        }
        var uriPath = endpoint.Authentication == null ? path : $"{path}.uri";
        if (consume)
        {
            if (!TransportAddress.TryParse(endpoint.Uri, true, out _, out var error)) errors.Add((uriPath, $"'{uriPath}' is not an address cloud events can be consumed from: {error}"));
        }
        else if (!TransportAddress.IsValidSink(endpoint.Uri, out var error)) errors.Add((uriPath, $"'{uriPath}' is not an address cloud events can be delivered to: {error}"));
        if (endpoint.Authentication != null) errors.AddRange(ValidateAuthentication(endpoint.Authentication, endpoint.Uri.IsAbsoluteUri ? endpoint.Uri.Scheme : null, $"{path}.authentication"));
        return errors;
    }

    /// <summary>
    /// Validates the specified authentication policy
    /// </summary>
    /// <param name="authentication">The policy to validate</param>
    /// <param name="scheme">The scheme of the address of the endpoint the policy authenticates to, if known</param>
    /// <param name="path">The path of the policy in the resource, used to key errors</param>
    /// <returns>The errors, keyed by the path of the property they concern, if any</returns>
    public static IReadOnlyList<(string Path, string Error)> ValidateAuthentication(AuthenticationPolicy authentication, string? scheme, string path)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        var errors = new List<(string, string)>();
        var policy = authentication.Policy;
        if (policy == null)
        {
            errors.Add((path, $"'{path}' must set exactly one of basic, bearer, apiKey, oauth2 or oidc"));
            return errors;
        }
        var policyPath = $"{path}.{authentication.Scheme}";
        var supported = TransportSchemes.GetAuthenticationSchemes(scheme);
        if (scheme != null && supported.Count > 0 && !supported.Contains(authentication.Scheme!)) errors.Add((policyPath, $"The '{scheme}' transport does not support the '{authentication.Scheme}' authentication: use one of {string.Join(", ", supported)}"));
        if (policy.Use != null)
        {
            if (string.IsNullOrWhiteSpace(policy.Use.Name)) errors.Add(($"{policyPath}.use", $"'{policyPath}.use' must name a secret"));
            // secrets are resolved within the namespace of the resource: a path such as '../other' could otherwise designate the secrets of another namespace (see #215)
            else if (!SecretReference.IsValidPath(policy.Use.Name)) errors.Add(($"{policyPath}.use", $"'{policy.Use.Name}' is not a valid secret path: {SecretReference.PathRules}"));
            if (policy.Use.Provider != null && !SecretProviders.AsEnumerable().Contains(policy.Use.Provider)) errors.Add(($"{policyPath}.use", $"'{policy.Use.Provider}' is not a secret provider: use one of {string.Join(", ", SecretProviders.AsEnumerable())}"));
            return errors;
        }
        switch (policy)
        {
            case BasicAuthenticationPolicy basic:
                if (string.IsNullOrWhiteSpace(basic.Username)) errors.Add(($"{policyPath}.username", $"'{policyPath}.username' must be set, unless a secret is used"));
                if (basic.Password == null) errors.Add(($"{policyPath}.password", $"'{policyPath}.password' must be set, unless a secret is used"));
                break;
            case BearerAuthenticationPolicy bearer:
                if (string.IsNullOrWhiteSpace(bearer.Token)) errors.Add(($"{policyPath}.token", $"'{policyPath}.token' must be set, unless a secret is used"));
                break;
            case ApiKeyAuthenticationPolicy apiKey:
                if (string.IsNullOrWhiteSpace(apiKey.Key)) errors.Add(($"{policyPath}.key", $"'{policyPath}.key' must be set, unless a secret is used"));
                if (apiKey.Header != null && !HeaderNamePattern().IsMatch(apiKey.Header)) errors.Add(($"{policyPath}.header", $"'{apiKey.Header}' is not a valid header name"));
                break;
            case OAuth2AuthenticationPolicyBase oauth2:
                errors.AddRange(ValidateOAuth2(oauth2, policyPath));
                break;
        }
        return errors;
    }

    static IEnumerable<(string, string)> ValidateOAuth2(OAuth2AuthenticationPolicyBase policy, string path)
    {
        if (policy.Authority == null || !policy.Authority.IsAbsoluteUri || policy.Authority.Scheme is not ("http" or "https")) yield return ($"{path}.authority", $"'{path}.authority' must be the absolute HTTP or HTTPS address of the authorization server");
        var grant = policy.Grant ?? OAuth2GrantType.ClientCredentials;
        if (!OAuth2GrantType.AsEnumerable().Contains(grant)) yield return ($"{path}.grant", $"'{grant}' is not a supported grant: use one of {string.Join(", ", OAuth2GrantType.AsEnumerable())}");
        if (string.IsNullOrWhiteSpace(policy.Client?.Id)) yield return ($"{path}.client.id", $"'{path}.client.id' must be set, unless a secret is used");
        if (grant == OAuth2GrantType.ClientCredentials && string.IsNullOrWhiteSpace(policy.Client?.Secret)) yield return ($"{path}.client.secret", $"'{path}.client.secret' must be set with the client_credentials grant, unless a secret is used");
        if (policy.Client?.Authentication is { } method && !OAuth2ClientAuthenticationMethod.AsEnumerable().Contains(method)) yield return ($"{path}.client.authentication", $"'{method}' is not a supported client authentication: use one of {string.Join(", ", OAuth2ClientAuthenticationMethod.AsEnumerable())}");
        if (grant == OAuth2GrantType.Password && (string.IsNullOrWhiteSpace(policy.Username) || policy.Password == null)) yield return (path, $"'{path}.username' and '{path}.password' must be set with the password grant, unless a secret is used");
    }

    [GeneratedRegex("^[!#$%&'*+.^_`|~0-9A-Za-z-]+$")]
    private static partial Regex HeaderNamePattern();

}
