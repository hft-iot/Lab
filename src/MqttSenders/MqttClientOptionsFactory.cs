using System.Security.Cryptography.X509Certificates;
using MQTTnet;

namespace MqttSenders
{
    internal static class MqttClientOptionsFactory
    {
        public static MqttClientOptions Create(MqttConnectionSettings settings)
        {
            var optionsBuilder = new MqttClientOptionsBuilder().WithTcpServer(
                settings.Host,
                settings.Port
            );

            if (!settings.UseTls)
            {
                return optionsBuilder.Build();
            }

            var caCertificatePath = ResolveRequiredPath(
                settings.CaCertificatePath,
                nameof(settings.CaCertificatePath)
            );
            var caCertificate = X509CertificateLoader.LoadCertificateFromFile(caCertificatePath);

            var tlsOptionsBuilder = new MqttClientTlsOptionsBuilder().UseTls();

            if (
                !string.IsNullOrWhiteSpace(settings.ClientCertificatePath)
                || !string.IsNullOrWhiteSpace(settings.ClientKeyPath)
            )
            {
                if (
                    string.IsNullOrWhiteSpace(settings.ClientCertificatePath)
                    || string.IsNullOrWhiteSpace(settings.ClientKeyPath)
                )
                {
                    throw new InvalidOperationException(
                        "Both client certificate paths must be configured when TLS client authentication is enabled."
                    );
                }

                var clientCertificate = LoadClientCertificate(
                    settings.ClientCertificatePath,
                    settings.ClientKeyPath
                );
                tlsOptionsBuilder.WithClientCertificates([clientCertificate]);
            }

            tlsOptionsBuilder.WithCertificateValidationHandler(context =>
                ValidateServerCertificate(context.Certificate, caCertificate)
            );

            optionsBuilder.WithTlsOptions(tlsOptionsBuilder.Build());

            return optionsBuilder.Build();
        }

        private static X509Certificate2 LoadClientCertificate(
            string clientCertificatePath,
            string clientKeyPath
        )
        {
            var resolvedCertificatePath = ResolveRequiredPath(
                clientCertificatePath,
                nameof(clientCertificatePath)
            );
            var resolvedKeyPath = ResolveRequiredPath(clientKeyPath, nameof(clientKeyPath));

            using var ephemeralCertificate = X509Certificate2.CreateFromPemFile(
                resolvedCertificatePath,
                resolvedKeyPath
            );

            return X509CertificateLoader.LoadPkcs12(
                ephemeralCertificate.Export(X509ContentType.Pkcs12),
                string.Empty,
                X509KeyStorageFlags.DefaultKeySet,
                null
            );
        }

        private static string ResolveRequiredPath(string? path, string settingName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException(
                    $"A value for '{settingName}' is required when TLS is enabled."
                );
            }

            var resolvedPath = ResolveExistingPath(path);

            if (resolvedPath is null)
            {
                throw new FileNotFoundException(
                    $"The configured certificate file '{path}' does not exist when resolved from either the current directory or the project root."
                );
            }

            return resolvedPath;
        }

        private static string? ResolveExistingPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return File.Exists(path) ? path : null;
            }

            var candidatePaths = new List<string>
            {
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path)),
            };

            var projectRoot = FindProjectRoot();
            if (projectRoot is not null)
            {
                candidatePaths.Add(Path.GetFullPath(Path.Combine(projectRoot, path)));
            }

            return candidatePaths.FirstOrDefault(File.Exists);
        }

        private static string? FindProjectRoot()
        {
            var projectFileName = $"{typeof(MqttClientOptionsFactory).Assembly.GetName().Name}.csproj";
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, projectFileName)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static bool ValidateServerCertificate(
            X509Certificate? certificate,
            X509Certificate2 trustedCaCertificate
        )
        {
            if (certificate is null)
            {
                return false;
            }

            using var serverCertificate = certificate as X509Certificate2
                ?? X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
            using var chain = new X509Chain();

            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(trustedCaCertificate);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            return chain.Build(serverCertificate);
        }
    }
}