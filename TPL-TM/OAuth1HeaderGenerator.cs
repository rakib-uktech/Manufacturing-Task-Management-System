using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Headers;
using System.Web;

namespace NetSuite
{
    public class OAuth1HeaderGenerator
    {
        private readonly string _consumerKey;
        private readonly string _consumerSecret;
        private readonly string _accessToken;
        private readonly string _tokenSecret;
        private readonly string _realm;

        public OAuth1HeaderGenerator(string consumerKey, string consumerSecret, string accessToken, string tokenSecret, string realm)
        {
            _consumerKey = consumerKey;
            _consumerSecret = consumerSecret;
            _accessToken = accessToken;
            _tokenSecret = tokenSecret;
            _realm = realm;
        }

        public AuthenticationHeaderValue Generate(string method, string url, IDictionary<string, string> additionalParams = null)
        {
            string nonce = GenerateNonce();
            string timestamp = GenerateTimestamp();

            // OAuth parameters
            var oauthParams = new SortedDictionary<string, string>
            {
                { "oauth_consumer_key", _consumerKey },
                { "oauth_token", _accessToken },
                { "oauth_signature_method", "HMAC-SHA256" },
                { "oauth_timestamp", timestamp },
                { "oauth_nonce", nonce },
                { "oauth_version", "1.0" }
            };

            // Parse query parameters from URL
            var uri = new Uri(url);
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            foreach (var key in queryParams.AllKeys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    oauthParams[key] = queryParams[key];
                }
            }

            // Include additional parameters
            if (additionalParams != null)
            {
                foreach (var param in additionalParams)
                {
                    oauthParams[param.Key] = param.Value;
                }
            }

            // Generate signature
            string baseUrl = uri.GetLeftPart(UriPartial.Path);
            string signatureBase = GenerateSignatureBase(method, baseUrl, oauthParams);
            string signingKey = $"{Uri.EscapeDataString(_consumerSecret)}&{Uri.EscapeDataString(_tokenSecret)}";
            string signature = GenerateSignature(signatureBase, signingKey);

            oauthParams["oauth_signature"] = signature;

            // Generate authorization header
            return new AuthenticationHeaderValue("OAuth", GenerateHeaderString(oauthParams));
        }

        private string GenerateSignatureBase(string method, string baseUrl, SortedDictionary<string, string> parameters)
        {
            var encodedParams = new StringBuilder();
            foreach (var param in parameters)
            {
                if (encodedParams.Length > 0) encodedParams.Append("&");
                encodedParams.Append($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}");
            }

            return $"{method.ToUpper()}&{Uri.EscapeDataString(baseUrl)}&{Uri.EscapeDataString(encodedParams.ToString())}";
        }

        private string GenerateSignature(string baseString, string signingKey)
        {
            using var hasher = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
            var hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(baseString));
            return Convert.ToBase64String(hash);
        }

        private string GenerateHeaderString(SortedDictionary<string, string> parameters)
        {
            var header = new StringBuilder();
            if (!string.IsNullOrEmpty(_realm))
            {
                header.Append($"realm=\"{Uri.EscapeDataString(_realm)}\", ");
            }
            foreach (var param in parameters)
            {
                if (param.Key.StartsWith("oauth_"))
                {
                    header.Append($"{param.Key}=\"{Uri.EscapeDataString(param.Value)}\", ");
                }
            }
            return header.ToString().TrimEnd(',', ' ');
        }

        private string GenerateNonce() => Guid.NewGuid().ToString("N");
        private string GenerateTimestamp() => ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
    }
}
