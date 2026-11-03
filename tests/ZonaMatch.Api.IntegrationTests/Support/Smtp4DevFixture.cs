using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using MimeKit;

namespace ZonaMatch.Api.IntegrationTests.Support;

public sealed class Smtp4DevFixture : IAsyncLifetime
{
    private const string CertificatePassword = "zonamatch-smtp-test-password";
    private readonly string _certificateDirectory = Path.Combine(Path.GetTempPath(), $"zonamatch-smtp-{Guid.NewGuid():N}");
    private readonly X509Certificate2 _certificate;
    private readonly IContainer _container;
    private readonly HttpClient _httpClient = new();

    public Smtp4DevFixture()
    {
        Directory.CreateDirectory(_certificateDirectory);
        var certificatePath = Path.Combine(_certificateDirectory, "smtp4dev.pfx");
        _certificate = CreateCertificate();
        File.WriteAllBytes(certificatePath, _certificate.Export(X509ContentType.Pfx, CertificatePassword));

        _container = new ContainerBuilder("rnwood/smtp4dev:v3")
            .WithPortBinding(25, true)
            .WithPortBinding(80, true)
            .WithBindMount(certificatePath, "/tmp/zonamatch-smtp-test.pfx", AccessMode.ReadOnly)
            .WithEnvironment("ServerOptions__HostName", "localhost")
            .WithEnvironment("ServerOptions__Port", "25")
            .WithEnvironment("ServerOptions__Urls", "http://*:80")
            .WithEnvironment("ServerOptions__TlsMode", "StartTls")
            .WithEnvironment("ServerOptions__TlsCertificate", "/tmp/zonamatch-smtp-test.pfx")
            .WithEnvironment("ServerOptions__TlsCertificatePassword", CertificatePassword)
            .WithEnvironment("ServerOptions__AuthenticationRequired", "false")
            .WithEnvironment("ServerOptions__SmtpAllowAnyCredentials", "true")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(25))
            .Build();

    }

    public string Host => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(25);

    public string CertificateThumbprint => _certificate.Thumbprint;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _httpClient.BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(80)}/");
        await WaitUntilReadyAsync();
    }

    public async Task<MimeMessage> GetLatestMessageAsync()
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            using var response = await _httpClient.GetAsync("api/Messages/new?pageSize=50");
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            var messages = document.RootElement;
            if (messages.GetArrayLength() > 0)
            {
                var messageId = messages[0].GetProperty("id").GetGuid();
                using var messageResponse = await _httpClient.GetAsync($"api/Messages/{messageId}/download");
                messageResponse.EnsureSuccessStatusCode();

                await using var messageStream = await messageResponse.Content.ReadAsStreamAsync();
                return await MimeMessage.LoadAsync(messageStream);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException("smtp4dev did not receive an email within 10 seconds.");
    }

    public async Task DisposeAsync()
    {
        _httpClient.Dispose();
        await _container.DisposeAsync();
        _certificate.Dispose();
        Directory.Delete(_certificateDirectory, recursive: true);
    }

    private async Task WaitUntilReadyAsync()
    {
        var timeout = Stopwatch.StartNew();

        while (timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(Host, Port, cancellationSource.Token);
                using var response = await _httpClient.GetAsync("api/Messages/new?pageSize=1", cancellationSource.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException or HttpRequestException)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException("smtp4dev did not become ready within 30 seconds.");
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
    }
}
