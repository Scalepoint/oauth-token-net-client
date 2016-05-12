﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace OAuthJwtAssertionTokenClient
{
    /// <summary>
    /// Token client that authenticates against the Authorization Server using "private_key_jwt" authentication method and retrieves access token
    /// </summary>
    public class JwtAssertionTokenClient
    {
        private readonly ITokenCache _cache;
        private static readonly Lazy<ITokenCache> DefaultTokenCache = new Lazy<ITokenCache>(() => new InMemoryTokenCache());
        private readonly PrivateKeyJwtClientCredentialsTokenClient _internalClient;
        private readonly string _partialCacheKey;

        /// <summary>
        /// Constructs a JwtAssertionTokenClient with specified options
        /// </summary>
        /// <param name="options">Token client options</param>
        public JwtAssertionTokenClient(TokenClientOptions options)
        {
            ValidateOptions(options);
            _cache = options.Cache ?? DefaultTokenCache.Value;
            _internalClient = new PrivateKeyJwtClientCredentialsTokenClient(options);
            _partialCacheKey = string.Join("|", options.TokenEndpointUrl, options.ClientId, options.Certificate.Thumbprint);
        }

        /// <summary>
        /// Constructs a JwtAssertionTokenClient with default (in-memory) cache
        /// </summary>
        /// <param name="tokenEndpointUrl">Authorization Server token endpoint</param>
        /// <param name="clientId">OAuth2 client_id</param>
        /// <param name="certificate">Certificate used for signing JWT client assertion (must have private key)</param>
        public JwtAssertionTokenClient(string tokenEndpointUrl, string clientId, X509Certificate2 certificate)
            : this(tokenEndpointUrl, clientId, certificate, DefaultTokenCache.Value)
        {
        }

        /// <summary>
        /// Constructs a JwtAssertionTokenClient with default (in-memory) cache
        /// </summary>
        /// <param name="tokenEndpointUrl">Authorization Server token endpoint</param>
        /// <param name="clientId">OAuth2 client_id</param>
        /// <param name="certificate">Certificate used for signing JWT client assertion (must have private key)</param>
        /// <param name="cache">Token cache</param>
        public JwtAssertionTokenClient(string tokenEndpointUrl, string clientId, X509Certificate2 certificate, ITokenCache cache)
            : this(new TokenClientOptions()
            {
                TokenEndpointUrl = tokenEndpointUrl,
                ClientId = clientId,
                Certificate = certificate,
                Cache = cache
            })
        {
        }

        private void ValidateOptions(TokenClientOptions options)
        {
            if (options == null)
            {
                throw new ArgumentException("Options cannot be null", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.TokenEndpointUrl))
            {
                throw new ArgumentException("TokenEndpointUrl must be set", nameof(options.TokenEndpointUrl));
            }

            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                throw new ArgumentException("ClientId must be set", nameof(options.ClientId));
            }

            if (options.Certificate == null)
            {
                throw new ArgumentException("Certificate must be set", nameof(options.Certificate));
            }

            ValidateCertificate(options.Certificate);
        }

        private static void ValidateCertificate(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                throw new ArgumentException("Certificate is required", nameof(certificate));
            }
            if (!certificate.HasPrivateKey)
            {
                throw new ArgumentException("Certificate has no private key and cannot be used for token signing", nameof(certificate));
            }
            try
            {
                // ReSharper disable once UnusedVariable
                var pk = certificate.PrivateKey;
            }
            catch (Exception e)
            {
                throw new ArgumentException(
                    "Certificate has a private key, but it cannot be accessed. " +
                    "Either user account has no permission to access private key or this is a CNG certificate. " +
                    "Only CSP certificates are fully supported by X509Certificate2.",
                    nameof(certificate),
                    e);
            }
        }

        /// <summary>
        /// Get access token
        /// </summary>
        /// <param name="scopes">Requested OAuth2 scopes</param>
        /// <returns>OAuth2 access token</returns>
        public async Task<string> GetAccessTokenAsync(params string[] scopes)
        {
            if (scopes == null || !scopes.Any())
            {
                throw new ArgumentException("At least one scope must be present", nameof(scopes));
            }
            var scopeString = string.Join("", scopes);
            var cacheKey = string.Join(":", _partialCacheKey, scopeString);
            return await _cache.GetAsync(cacheKey, () => _internalClient.GetToken(scopes));
        }

        /// <summary>
        /// Get access token
        /// </summary>
        /// <param name="scopes">Requested OAuth2 scopes</param>
        /// <returns>OAuth2 access token</returns>
        public Task<string> GetAccessTokenAsync(IEnumerable<string> scopes)
        {
            return GetAccessTokenAsync(scopes?.ToArray());
        }
    }
}
