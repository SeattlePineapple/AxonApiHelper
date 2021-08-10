using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AxonApiHelper
{
    public class Endpoint
    {
        private readonly string PartnerID;
        private readonly string ClientID;
        private readonly string ClientSecret;
        private readonly string ActiveToken;

        private const string PartnerID1 = "123";
        private const string ClientID1 = "456";
        private const string ClientSecret1 = "789";

        private const string PartnerID2 = "123";
        private const string ClientID2 = "456";
        private const string ClientSecret2 = "789";

        private const string baseUrl = "https://api.evidence.com/api/";
        private static readonly string tokenUrl = $"{baseUrl}oauth2/token";

        private readonly string logFolder;

        private static Endpoint _instance;
        private static Endpoint Instance => _instance ??= new(AxonEnvironment.Environment1, false);

        private readonly bool EnableLogging;

        private Endpoint(AxonEnvironment environment, bool enableLogging)
        {
            switch (environment)
            {
                case AxonEnvironment.Environment1:
                    PartnerID = PartnerID1;
                    ClientID = ClientID1;
                    ClientSecret = ClientSecret1;
                    break;
                case AxonEnvironment.Environment2:
                    PartnerID = PartnerID2;
                    ClientID = ClientID2;
                    ClientSecret = ClientSecret2;
                    break;
            }

            //check to see if valid bearer token exists
            RegistryKey axonKey = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Axon", true);
            if (axonKey == null)
            {
                try
                {
                    axonKey = Registry.LocalMachine.OpenSubKey("SOFTWARE", true).CreateSubKey("Axon", true);
                }
                catch
                {
                    Console.WriteLine("Must run with an Administrator account");
                    Environment.Exit(1);
                }
            }
            string[] values = axonKey.GetValueNames();
            if (values.Contains($"token{environment}") && values.Contains($"expire{environment}"))
            {
                object tokenVal = axonKey.GetValue($"token{environment}");
                object expireVal = axonKey.GetValue($"expire{environment}");
                if (tokenVal != null && expireVal != null)
                {
                    try
                    {
                        if (DateTime.FromBinary((long)expireVal).CompareTo(DateTime.Now) > 0)
                        {
                            ActiveToken = (string)tokenVal;
                        }
                    }
                    catch { }
                }
            }
            // otherwise, get new token
            if (ActiveToken == null)
            {
                ActiveToken = GenerateAccessToken().WaitForResult();
                try
                {
                    axonKey.SetValue($"token{environment}", ActiveToken);
                    axonKey.SetValue($"expire{environment}", DateTime.Now.AddHours(3.75).ToBinary(), RegistryValueKind.QWord); // tokens expire after 4 hours
                }
                catch
                {
                    Console.WriteLine("Must run with an Administrator account");
                    Environment.Exit(1);
                }
            }

            logFolder = @"C:\temp\" + DateTime.Now.ToShortDateString().Replace('/', '-');
            Directory.CreateDirectory(logFolder);
            EnableLogging = enableLogging;

            Console.WriteLine($"{environment} Endpoint Established");
        }

        public static string Get(string url, int version = 1)
        {
            return CSHelpers.Retry(() =>
            {
                using HttpClient client = Instance.GetDefaultHttpClient();
                HttpResponseMessage response = client.GetAsync($"{baseUrl}v{version}/agencies/{Instance.PartnerID}/{url}").WaitForResult();
                return Instance.Log(response.Content.ReadAsStringAsync().WaitForResult());
            }, 3);
        }

        public static bool DownloadBytes(string fileName, string url, int version = 1)
        {
            using (HttpClient client = Instance.GetDefaultHttpClient())
            using (Stream s = client.GetStreamAsync($"{baseUrl}v{version}/agencies/{Instance.PartnerID}/{url}").WaitForResult())
            using (FileStream f = new(fileName, FileMode.Create))
            {
                try
                {
                    s.CopyTo(f);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Download Interrupted: {e.Message}");
                    f.Close();
                    f.Dispose();
                    File.Delete(fileName);
                    return false;
                }
            }
            return true;
        }

        public static string Post(string url, string content, int version = 1)
        {
            using HttpClient client = Instance.GetDefaultHttpClient();
            HttpResponseMessage response = client.PostAsync($"{baseUrl}v{version}/agencies/{Instance.PartnerID}/{url}", new StringContent(content)).WaitForResult();
            return Instance.Log(response.Content.ReadAsStringAsync().WaitForResult());
        }

        public static string Patch(string url, string content, int version = 1)
        {
            using HttpClient client = Instance.GetDefaultHttpClient();
            HttpResponseMessage response = client.PatchAsync($"{baseUrl}v{version}/agencies/{Instance.PartnerID}/{url}", new StringContent(content)).WaitForResult();
            return Instance.Log(response.Content.ReadAsStringAsync().WaitForResult());
        }

        public static string Delete(string url, string content, int version = 1)
        {
            using HttpClient client = Instance.GetDefaultHttpClient();
            HttpRequestMessage message = new(HttpMethod.Delete, $"{baseUrl}v{version}/agencies/{Instance.PartnerID}/{url}");
            message.Content = new StringContent(content);
            HttpResponseMessage response = client.SendAsync(message).WaitForResult();
            return Instance.Log(response.Content.ReadAsStringAsync().WaitForResult());
        }

        private async Task<string> GenerateAccessToken()
        {
            HttpClient client = new(new HttpClientHandler() { UseDefaultCredentials = false });
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

            List<KeyValuePair<string, string>> postData = new()
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("partner_id", PartnerID),
                new KeyValuePair<string, string>("client_id", ClientID),
                new KeyValuePair<string, string>("client_secret", ClientSecret)
            };

            HttpResponseMessage tokenResponse = client.PostAsync(tokenUrl, new FormUrlEncodedContent(postData)).Result;
            return (await tokenResponse.Content.ReadAsAsync<AccessTokenResponse>(new[] { new JsonMediaTypeFormatter() }))?.AccessToken;
        }

        private HttpClient GetDefaultHttpClient()
        {
            HttpClient client = new(new HttpClientHandler() { UseDefaultCredentials = false });
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ActiveToken}");
            return client;
        }

        private string Log(string content)
        {
            if (EnableLogging)
            {
                File.WriteAllText($@"{logFolder}\{Guid.NewGuid()}.json", content);
            }
            return content;
        }

        private enum AxonEnvironment
        {
            Environment1, Environment2
        }
    }
}
