using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KBroker
{
    public class KrakenApi
    {
        private const string BaseDomain = "https://api.kraken.com";
        private const string PrivatePath = "/0/private/";
        private const string PublicPath = "/0/public/";

        private static long LastUsedNonce;
        public static string ApiPrivateKey;
        public static string ApiPublicKey;

        public static async Task<string> QueryPublicEndpoint(string endpointName, string inputParameters = null)
        {
            string jsonData;
            string query = !String.IsNullOrEmpty(inputParameters) ? $"?{inputParameters}" : string.Empty;
            string apiEndpointFullURL = BaseDomain + PublicPath + endpointName + query;
            using (HttpClient client = new HttpClient())
            {
                jsonData = await client.GetStringAsync(apiEndpointFullURL);
            }
            return jsonData;
        }

        public static async Task<string> QueryPrivateEndpoint(string endpointName, string inputParameters)
        {           
            string apiEndpointFullURL = BaseDomain + PrivatePath + endpointName;
            string nonce = GetNextNonce();
            if (string.IsNullOrWhiteSpace(inputParameters) == false)
            {
                inputParameters = "&" + inputParameters;
            }
            string apiPostBodyData = "nonce=" + nonce + inputParameters;
            string signature = CreateAuthenticationSignature(endpointName, nonce, inputParameters);
            string jsonData;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("API-Key", ApiPublicKey);
                client.DefaultRequestHeaders.Add("API-Sign", signature);
                client.DefaultRequestHeaders.Add("User-Agent", "KrakenDotNet Client");
                StringContent data = new StringContent(apiPostBodyData, Encoding.UTF8, "application/x-www-form-urlencoded");
                HttpResponseMessage response = await client.PostAsync(apiEndpointFullURL, data);
                jsonData = response.Content.ReadAsStringAsync().Result;
            }
            Logger.AddEntry($"apiPostBodyData: {apiPostBodyData}");
            return jsonData;
        }

        private static string GetNextNonce()
        {
            var nonce = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (nonce == LastUsedNonce) nonce += 1;
            LastUsedNonce = nonce;
            return nonce.ToString();
        }

        public static string CreateAuthenticationSignature(string endpointName, string nonce, string inputParams)
        {
            byte[] sha256Hash = ComputeSha256Hash(nonce, inputParams);
            byte[] sha512Hash = ComputeSha512Hash(sha256Hash, endpointName);
            string signatureString = Convert.ToBase64String(sha512Hash);
            return signatureString;
        }

        private static byte[] ComputeSha256Hash(string nonce, string inputParams)
        {
            byte[] sha256Hash;
            string sha256HashData = nonce.ToString() + "nonce=" + nonce.ToString() + inputParams;
            using (var sha = SHA256.Create())
            {
                sha256Hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sha256HashData));
            }
            return sha256Hash;
        }

        private static byte[] ComputeSha512Hash(byte[] sha256Hash, string endpointName)
        {
            string apiEndpointPath = PrivatePath + endpointName;
            byte[] apiEndpointPathBytes = Encoding.UTF8.GetBytes(apiEndpointPath);
            byte[] sha512HashData = apiEndpointPathBytes.Concat(sha256Hash).ToArray();
            HMACSHA512 encryptor = new HMACSHA512(Convert.FromBase64String(ApiPrivateKey));
            byte[] sha512Hash = encryptor.ComputeHash(sha512HashData);
            return sha512Hash;
        }

        public static void ClearApiKeys()
        {
            ApiPrivateKey = null;
            ApiPublicKey = null;
        }
    }
}
