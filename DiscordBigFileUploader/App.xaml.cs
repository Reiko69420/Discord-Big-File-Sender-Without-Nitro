using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace DiscordBigFileUploader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public class HttpUtil
        {
            private static readonly HttpClient client = new HttpClient();

            public static async Task<(string, HttpResponseMessage)> GetAsync(string url, string finalToken)
            {
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "Discord-Android/172024");
                client.DefaultRequestHeaders.Add("authorization", finalToken);
                client.DefaultRequestHeaders.Add("x-super-properties", "eyJvcyI6IkFuZHJvaWQiLCJicm93c2VyIjoiRGlzY29yZCBBbmRyb2lkIiwiZGV2aWNlIjoibXVuY2giLCJzeXN0ZW1fbG9jYWxlIjoiZnItRlIiLCJjbGllbnRfdmVyc2lvbiI6IjE3Mi4yNCAtIHJuIiwicmVsZWFzZV9jaGFubmVsIjoiZ29vZ2xlUmVsZWFzZSIsImRldmljZV92ZW5kb3JfaWQiOiIzZDk4NGYxYS02NTYwLTQ2ZjktOWZhNy0zNGU2YzEzNmQyNmUiLCJicm93c2VyX3VzZXJfYWdlbnQiOiIiLCJicm93c2VyX3ZlcnNpb24iOiIiLCJvc192ZXJzaW9uIjoiMzMiLCJjbGllbnRfYnVpbGRfbnVtYmVyIjo2OTY5Njk2OTY5Njk2LCJjbGllbnRfZXZlbnRfc291cmNlIjpudWxsLCJkZXNpZ25faWQiOjB9");

                HttpResponseMessage response = await client.GetAsync(url);

                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                return (responseBody, response);
            }

            public static async Task<(string, HttpResponseMessage)> PostAsync(string url, string finalToken, HttpContent content)
            {
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "Discord-Android/172024");
                client.DefaultRequestHeaders.Add("authorization", finalToken);
                client.DefaultRequestHeaders.Add("x-super-properties", "eyJvcyI6IkFuZHJvaWQiLCJicm93c2VyIjoiRGlzY29yZCBBbmRyb2lkIiwiZGV2aWNlIjoibXVuY2giLCJzeXN0ZW1fbG9jYWxlIjoiZnItRlIiLCJjbGllbnRfdmVyc2lvbiI6IjE3Mi4yNCAtIHJuIiwicmVsZWFzZV9jaGFubmVsIjoiZ29vZ2xlUmVsZWFzZSIsImRldmljZV92ZW5kb3JfaWQiOiIzZDk4NGYxYS02NTYwLTQ2ZjktOWZhNy0zNGU2YzEzNmQyNmUiLCJicm93c2VyX3VzZXJfYWdlbnQiOiIiLCJicm93c2VyX3ZlcnNpb24iOiIiLCJvc192ZXJzaW9uIjoiMzMiLCJjbGllbnRfYnVpbGRfbnVtYmVyIjo2OTY5Njk2OTY5Njk2LCJjbGllbnRfZXZlbnRfc291cmNlIjpudWxsLCJkZXNpZ25faWQiOjB9");

                HttpResponseMessage response = await client.PostAsync(url, content);

                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                return (responseBody, response);
            }

            public static async Task<HttpResponseMessage> PutAsync(string url, ByteArrayContent content)
            {
                client.DefaultRequestHeaders.Clear();
                HttpResponseMessage response = await client.PutAsync(url, content);

                if (response.IsSuccessStatusCode)
                    return response;
                return null;
            }
        }
    }
}
