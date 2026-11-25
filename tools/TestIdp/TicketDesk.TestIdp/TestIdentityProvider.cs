using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace TicketDesk.TestIdp;

/// <summary>An account the test provider can sign in, with the claims Entra ID puts in an id token (oid, tid, roles).</summary>
public sealed record TestAccount(string Oid, string PreferredUsername, string Name, string? Email = null, string[]? Roles = null)
{
    public string[] AppRoles => Roles ?? [];
}

/// <summary>
/// A minimal OpenID Connect provider standing in for Entra ID in automated tests and lab runs: discovery, JWKS,
/// authorization endpoint (code flow, form_post, PKCE S256 required), token endpoint (client id + optional secret), RS256
/// id tokens with <c>oid</c>, <c>tid</c>, <c>preferred_username</c>, <c>email</c>, <c>name</c> and app <c>roles</c>.
/// The account is chosen with <c>login_hint</c> (its preferred_username or oid) or on a small chooser page.
/// </summary>
public sealed class TestIdentityProvider
{
    private readonly RSA _key = RSA.Create(2048);
    private readonly string _kid = Guid.NewGuid().ToString("N")[..16];
    private readonly ConcurrentDictionary<string, (TestAccount Account, string ClientId, string RedirectUri, string? Nonce, string Challenge)> _codes = new();

    public TestIdentityProvider(string tenantId, string clientId, string? clientSecret, IEnumerable<TestAccount> accounts)
    {
        TenantId = tenantId;
        ClientId = clientId;
        ClientSecret = clientSecret;
        Accounts = accounts.ToList();
    }

    public string TenantId { get; }
    public string ClientId { get; }
    public string? ClientSecret { get; }
    public List<TestAccount> Accounts { get; }

    /// <summary>Tokens issued (for assertions).</summary>
    public int TokensIssued;

    private static string Issuer(HttpRequest r, string tenant) => $"{r.Scheme}://{r.Host}/{tenant}/v2.0";

    public void Map(IEndpointRouteBuilder app)
    {
        var t = "/" + TenantId + "/v2.0";
        app.MapGet(t + "/.well-known/openid-configuration", (HttpRequest r) =>
        {
            var iss = Issuer(r, TenantId);
            return Results.Json(new Dictionary<string, object>
            {
                ["issuer"] = iss,
                ["authorization_endpoint"] = iss + "/authorize",
                ["token_endpoint"] = iss + "/token",
                ["jwks_uri"] = iss + "/keys",
                ["end_session_endpoint"] = iss + "/logout",
                ["response_types_supported"] = new[] { "code" },
                ["response_modes_supported"] = new[] { "form_post", "query" },
                ["subject_types_supported"] = new[] { "pairwise" },
                ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
                ["code_challenge_methods_supported"] = new[] { "S256" },
                ["scopes_supported"] = new[] { "openid", "profile", "email" },
                ["claims_supported"] = new[] { "sub", "oid", "tid", "name", "preferred_username", "email", "roles", "nonce" },
            });
        });
        app.MapGet(t + "/keys", () =>
        {
            var p = _key.ExportParameters(false);
            return Results.Json(new { keys = new[] { new { kty = "RSA", use = "sig", alg = "RS256", kid = _kid, n = B64(p.Modulus!), e = B64(p.Exponent!) } } });
        });
        app.MapGet(t + "/authorize", Authorize);
        app.MapPost(t + "/authorize", Authorize);
        app.MapPost(t + "/token", Token);
        app.MapGet(t + "/logout", (HttpRequest r) => Results.Redirect(r.Query["post_logout_redirect_uri"].FirstOrDefault() ?? "/"));
    }

    private IResult Authorize(HttpRequest r)
    {
        var q = r.HasFormContentType ? r.Form.ToDictionary(k => k.Key, k => k.Value.ToString()) : r.Query.ToDictionary(k => k.Key, k => k.Value.ToString());
        string? Q(string k) => q.TryGetValue(k, out var v) && v.Length > 0 ? v : null;
        if (Q("client_id") != ClientId) return Results.BadRequest("unknown client_id");
        if (Q("response_type") != "code") return Results.BadRequest("only response_type=code");
        if (Q("code_challenge_method") != "S256" || Q("code_challenge") is null) return Results.BadRequest("PKCE S256 required");
        var redirect = Q("redirect_uri");
        if (redirect is null) return Results.BadRequest("redirect_uri required");
        var hint = Q("login_hint") ?? Q("account");
        var account = hint is null ? null : Accounts.FirstOrDefault(a => a.PreferredUsername.Equals(hint, StringComparison.OrdinalIgnoreCase) || a.Oid == hint);
        if (account is null)
        {
            // chooser: one button per account, re-posting the request with account=<oid>
            var hidden = string.Concat(q.Where(kv => kv.Key != "account").Select(kv => $"<input type=\"hidden\" name=\"{H(kv.Key)}\" value=\"{H(kv.Value)}\">"));
            var buttons = string.Concat(Accounts.Select(a => $"<p><button name=\"account\" value=\"{H(a.Oid)}\">{H(a.Name)} ({H(a.PreferredUsername)}; roles: {H(string.Join(", ", a.AppRoles))})</button></p>"));
            return Results.Content($"<!DOCTYPE html><html><head><title>Test identity provider</title></head><body><h1>Test identity provider (not Entra ID)</h1><form method=\"post\">{hidden}{buttons}</form></body></html>", "text/html");
        }
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        _codes[code] = (account, ClientId, redirect, Q("nonce"), Q("code_challenge")!);
        var state = Q("state") ?? "";
        if (Q("response_mode") == "query")
            return Results.Redirect(redirect + (redirect.Contains('?') ? "&" : "?") + $"code={code}&state={Uri.EscapeDataString(state)}");
        return Results.Content("<!DOCTYPE html><html><head><title>Working...</title></head><body onload=\"document.forms[0].submit()\">" +
                               $"<form method=\"post\" action=\"{H(redirect)}\"><input type=\"hidden\" name=\"code\" value=\"{code}\"><input type=\"hidden\" name=\"state\" value=\"{H(state)}\">" +
                               "<noscript><button>Continue</button></noscript></form></body></html>", "text/html");
    }

    private async Task<IResult> Token(HttpRequest r)
    {
        var f = await r.ReadFormAsync();
        var (clientId, secret) = (f["client_id"].ToString(), f["client_secret"].ToString());
        if (r.Headers.Authorization.ToString() is { Length: > 6 } auth && auth.StartsWith("Basic ", StringComparison.Ordinal))
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(auth[6..])).Split(':', 2);
            (clientId, secret) = (WebUtility.UrlDecode(parts[0]), WebUtility.UrlDecode(parts[1]));
        }
        if (clientId != ClientId || (ClientSecret != null && secret != ClientSecret)) return Results.Json(new { error = "invalid_client" }, statusCode: 401);
        if (f["grant_type"] != "authorization_code" || !_codes.TryRemove(f["code"].ToString(), out var c)) return Results.Json(new { error = "invalid_grant" }, statusCode: 400);
        if (c.RedirectUri != f["redirect_uri"].ToString()) return Results.Json(new { error = "invalid_grant", error_description = "redirect_uri" }, statusCode: 400);
        var verifier = f["code_verifier"].ToString();
        if (B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))) != c.Challenge) return Results.Json(new { error = "invalid_grant", error_description = "PKCE" }, statusCode: 400);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var a = c.Account;
        var claims = new JsonObject
        {
            ["iss"] = Issuer(r, TenantId), ["aud"] = ClientId, ["iat"] = now, ["nbf"] = now, ["exp"] = now + 3600,
            ["sub"] = "sub-" + a.Oid, ["oid"] = a.Oid, ["tid"] = TenantId, ["name"] = a.Name, ["preferred_username"] = a.PreferredUsername,
            ["ver"] = "2.0",
        };
        if (a.Email != null) claims["email"] = a.Email;
        if (c.Nonce != null) claims["nonce"] = c.Nonce;
        if (a.AppRoles.Length > 0) claims["roles"] = new JsonArray(a.AppRoles.Select(x => (JsonNode)x).ToArray());
        Interlocked.Increment(ref TokensIssued);
        return Results.Json(new { token_type = "Bearer", expires_in = 3600, id_token = Sign(claims), access_token = "test-idp-access-token-" + Guid.NewGuid().ToString("N") });
    }

    private string Sign(JsonObject payload)
    {
        var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT", kid = _kid }));
        var body = B64(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        var sig = _key.SignData(Encoding.ASCII.GetBytes(header + "." + body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return header + "." + body + "." + B64(sig);
    }

    private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string H(string s) => WebUtility.HtmlEncode(s);
}
