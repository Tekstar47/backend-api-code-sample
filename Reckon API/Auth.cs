using System;
using System.Collections.Generic;

namespace SPL_API.Auth
{
    internal static class SPLAuth
    {
        private static List<User> users = new() {
            new() {
                UserName = "CartonCloudAPIWebhook",
                ClientID = Environment.GetEnvironmentVariable("CARTON_CLOUD_CLIENT_ID"),
                ClientSecret = Environment.GetEnvironmentVariable("CARTON_CLOUD_CLIENT_SECRET"),
                Active = true,
            }
        };

        public static User ValidateBearerToken(string bearerToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(bearerToken))
                {
                    // TODO: throw error and further validate bearer token
                    return null;
                }
                string encodedToken = bearerToken.Split(" ")[1];
                string decodedString = AppUtilities.Base64Decode(encodedToken);
                if (string.IsNullOrWhiteSpace(decodedString))
                {
                    // TODO: further validate bearer token is a clientid + secret
                    return null;
                }

                string[] split = decodedString.Split(":");
                string clientID = split[0];
                string clientSecret = split[1];


                // TODO: Database lookup by clientID + clientSecret
                return users.Find(x => x.ClientID == clientID && x.ClientSecret == clientSecret); ;
            }
            catch { return null; }
        }
    }

    class User
    {
        public string UserName { get; set; } = string.Empty;
        public string ClientID { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public bool Active { get; set; } = false;
    }
}
