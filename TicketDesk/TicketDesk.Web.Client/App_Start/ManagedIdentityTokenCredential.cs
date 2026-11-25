// Added for the AKS round (Data Protection key ring wrapped with a Key Vault key). Distributed under the Ms-PL like the rest of TicketDesk.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using TicketDesk.Domain.Infrastructure;

namespace TicketDesk.Web.Client
{
    /// <summary>
    /// <see cref="TokenCredential"/> over the VM's managed identity (IMDS, <see cref="ManagedIdentityTokens"/>), so the Azure
    /// Key Vault key-wrap package needs no Azure.Identity in this net48 app. Scopes are mapped to the IMDS resource
    /// (<c>https://vault.azure.net/.default</c> → <c>https://vault.azure.net</c>).
    /// </summary>
    public sealed class ManagedIdentityTokenCredential : TokenCredential
    {
        private readonly ManagedIdentityTokens _tokens;

        public ManagedIdentityTokenCredential(string clientId)
        {
            _tokens = new ManagedIdentityTokens(clientId);
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            if (requestContext.Scopes == null || requestContext.Scopes.Length != 1)
            {
                throw new ArgumentException("exactly one scope is expected", "requestContext");
            }
            var scope = requestContext.Scopes[0];
            var resource = scope.EndsWith("/.default", StringComparison.Ordinal) ? scope.Substring(0, scope.Length - "/.default".Length) : scope;
            var token = _tokens.GetToken(resource);
            return new AccessToken(token.Item1, token.Item2);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new ValueTask<AccessToken>(GetToken(requestContext, cancellationToken));
        }
    }
}
