// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Testing;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Interop.FunctionalTests
{
    /// <summary>
    /// This tests interop with System.Net.Http.HttpClient (SocketHttpHandler) using HTTP/2 (H2 and H2C)
    /// </summary>
    public class HttpClientHttp2InteropTests : LoggedTest
    {
        public HttpClientHttp2InteropTests()
        {
            // H2C
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        public static IEnumerable<object[]> SupportedSchemes
        {
            get
            {
                var list = new List<object[]>()
                {
                    new[] { "http" }
                };

                var supportsAlpn =
                    // "Missing Windows ALPN support: https://en.wikipedia.org/wiki/Application-Layer_Protocol_Negotiation#Support"
                    new MinimumOSVersionAttribute(OperatingSystems.Windows, WindowsVersions.Win81).IsMet
                    // "Missing SslStream ALPN support: https://github.com/dotnet/corefx/issues/30492"
                    || new OSSkipConditionAttribute(OperatingSystems.MacOSX).IsMet
                    // Debian 8 uses OpenSSL 1.0.1 which does not support ALPN
                    || new SkipOnHelixAttribute("https://github.com/aspnet/AspNetCore/issues/10428") { Queues = "Debian.8.Amd64.Open" }.IsMet;

                // https://github.com/aspnet/AspNetCore/issues/11301 We should use Skip but it's broken at the moment.
                if (supportsAlpn)
                {
                    list.Add(new[] { "https" });
                }

                return list;
            }
        }

        [ConditionalTheory]
        [MemberData(nameof(SupportedSchemes))]
        public async Task Test(string scheme)
        {
            var hostBuilder = new HostBuilder()
                .ConfigureWebHost(webHostBuilder =>
                {
                    webHostBuilder.UseKestrel(options =>
                    {
                        options.Listen(IPAddress.Loopback, 0, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http2;
                            if (scheme == "https")
                            {
                                listenOptions.UseHttps(TestResources.GetTestCertificate());
                            }
                        });
                    })
                    .ConfigureServices(AddTestLogging)
                    .Configure(app => app.Run(context => context.Response.WriteAsync("Hello World")));
                });
            using var host = await hostBuilder.StartAsync();

            var url = $"{scheme}://127.0.0.1:{host.GetPort().ToString(CultureInfo.InvariantCulture)}/";

            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            using var client = new HttpClient(handler);
            client.DefaultRequestVersion = HttpVersion.Version20;
            var response = await client.GetAsync(url);
            Assert.Equal(HttpVersion.Version20, response.Version);
            Assert.Equal("Hello World", await response.Content.ReadAsStringAsync());
        }
    }
}
