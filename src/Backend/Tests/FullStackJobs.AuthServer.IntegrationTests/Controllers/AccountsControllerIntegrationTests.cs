using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FullStackJobs.AuthServer.IntegrationTests.Shared;
using FullStackJobs.AuthServer.Models;
using Newtonsoft.Json;
using PuppeteerSharp;
using Xunit;

namespace FullStackJobs.AuthServer.IntegrationTests.Controllers
{
    [Collection("WebHost collection")]
    public class AccountsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private static readonly IList<SignupRequest> _signupRequests = new List<SignupRequest>
        {
            new SignupRequest() {FullName = "Mark Macneil", Email = "mark@fullstackmark.com", Password="Pa$$w0rd!", Role="applicant"},
            new SignupRequest() {FullName = "Prescott Terrell", Email = "pterrell@mailinator.com", Password="Pa$$w0rd!", Role="employer"}
        };
       
        private readonly HttpClient _client;

        public AccountsControllerIntegrationTests(CustomWebApplicationFactory<Startup> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CanCreateAccount()
        {
            var httpResponse = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/accounts")
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(_signupRequests[0]), Encoding.UTF8, "application/json")
            });

            httpResponse.EnsureSuccessStatusCode();

            // Deserialize and examine results.
            var stringResponse = await httpResponse.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<SignupResponse>(stringResponse);
            Assert.Equal(_signupRequests[0].FullName, response.FullName);
            Assert.Equal(_signupRequests[0].Email, response.Email);
            Assert.Equal(_signupRequests[0].Role, response.Role);
            Assert.True(Guid.TryParse(response.Id, out _));
        }     

        [Fact]
        public async Task CanLogin()
        {
            var httpResponse = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/accounts")
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(_signupRequests[1]), Encoding.UTF8, "application/json")
            });

            httpResponse.EnsureSuccessStatusCode();

            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);

            using (var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true }))
            {
                using (var page = await browser.NewPageAsync())
                {
                    await page.GoToAsync($"{Constants.HostAddress}/test-client/index.html");

                    var navigationTask = page.WaitForNavigationAsync();

                    await Task.WhenAll(navigationTask, page.ClickAsync("button"));

                    await page.TypeAsync("#Username", _signupRequests[1].Email);
                    await page.TypeAsync("#Password", _signupRequests[1].Password);

                    navigationTask = page.WaitForNavigationAsync(new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });
                    await Task.WhenAll(navigationTask, page.ClickAsync(".btn-primary"));

                    var content = await page.GetContentAsync();
                    await page.CloseAsync();

                    Assert.Contains("User logged in", content);
                    Assert.Contains("Prescott Terrell", content);
                    Assert.Contains("pterrell@mailinator.com", content);
                    Assert.Contains("employer", content);
                }
            };
        }
    }
}
