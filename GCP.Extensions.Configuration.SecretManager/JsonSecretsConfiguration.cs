using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.SecretManager.V1;
using Google.Api.Gax.ResourceNames;

namespace GCP.Extensions.Configuration.SecretManager
{
    /// <summary>Options for configuration of the GCPJsonSecretsConfigurationProvider</summary>
    public class GcpJsonSecretOptions {
    /// <summary>(optinal) the GoogleCredential to use.</summary>
        public GoogleCredential GoogleCredential {get;set;}
    /// <summary>(optional) the SecretManagerServiceClient to use. if specified GoogleCredential is ignored.</summary>
        public SecretManagerServiceClient SecretMangerClient { get; set; }
    /// <summary>(optional) the ProjectId. if ommited, ProjectId will be dirived from Credential or Environment</summary>
        public string ProjectId { get; set; }
    /// <summary>(optional) a filter for the secrets list. filter rules: https://cloud.google.com/secret-manager/docs/filtering</summary>
        public string ListFilter { get; set; }
    }

    public class GcpJsonSecretsConfigurationProvider : JsonStreamConfigurationProvider
    {
        private readonly GcpJsonSecretOptions _options;

        internal SecretManagerServiceClient BuildClient(ICredential credential) {
            if (null == credential) {
                _options.ProjectId ??= Helpers.GetProjectId();
                return SecretManagerServiceClient.Create();
            }
            if (credential is ServiceAccountCredential sac) { _options.ProjectId ??= sac.ProjectId ?? Helpers.GetProjectId(); }
            else { _options.ProjectId ??= Helpers.GetProjectId(); }

            var builder = new SecretManagerServiceClientBuilder
            {
                TokenAccessMethod = credential.GetAccessTokenForRequestAsync
            };
            return builder.Build();
        }

        internal string ReplacePathSeparator(string path, string oldSeparator = Helpers.DoubleUnderscore)
        {
            string result = path.Replace(oldSeparator, ConfigurationPath.KeyDelimiter);
            return result;
        }

        public GcpJsonSecretsConfigurationProvider(GcpJsonSecretsConfigurationSource source, GcpJsonSecretOptions options) : base(source) {
            if (null == options) { throw new System.ArgumentNullException(nameof(options)); }
            _options = options;
            if (null == _options.SecretMangerClient) {
                _options.GoogleCredential ??= GoogleCredential.GetApplicationDefault();
                _options.SecretMangerClient = BuildClient(_options.GoogleCredential.UnderlyingCredential);
            }
        }

        public override void Load()
        {
            if (string.IsNullOrWhiteSpace(_options.ProjectId)) { throw new System.ArgumentOutOfRangeException("ProjectId could not be determined from environment and no override specified."); }

            ProjectName projectName = new ProjectName(_options.ProjectId);
            ListSecretsRequest request = new ListSecretsRequest() { ParentAsProjectName = projectName, Filter = (_options.ListFilter ?? string.Empty) };
            var secrets = _options.SecretMangerClient.ListSecrets(request);

            var secret = secrets.FirstOrDefault();
            if (null == secret) { return; }

            ListSecretVersionsRequest versionsRequest = new ListSecretVersionsRequest() {
                Filter = Helpers.FilterVersions_Enabled, ParentAsSecretName = secret.SecretName
            };
            var versions = _options.SecretMangerClient.ListSecretVersions(versionsRequest).OrderByDescending(v => v.CreateTime);
            var ver = versions.FirstOrDefault();
            if (null == ver) { return; }

            var secretVersion = _options.SecretMangerClient.AccessSecretVersion(ver.SecretVersionName);
            System.IO.Stream stream = new System.IO.MemoryStream(secretVersion?.Payload.Data.ToByteArray());
            base.Load(stream);
        }
    }

    public class GcpJsonSecretsConfigurationSource : JsonStreamConfigurationSource
    {
        private GcpJsonSecretOptions _options;

        public GcpJsonSecretsConfigurationSource(string listFilter = null, GoogleCredential googleCredential = null, string projectId = null)
        {
            _options = new GcpJsonSecretOptions();
            _options.GoogleCredential = googleCredential;
            _options.ListFilter = listFilter;
            _options.ProjectId = projectId;
        }

        public GcpJsonSecretsConfigurationSource(GcpJsonSecretOptions options) {
            if (null == options) { throw new System.ArgumentNullException(nameof(options)); }
            _options = options;
        }

        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new GcpJsonSecretsConfigurationProvider(this, _options);
        }
    }

    public static class JsonConfigurationExtensions
    {
        public static IConfigurationBuilder AddGcpJsonSecrets (this IConfigurationBuilder builder, string listFilter = null
            , GoogleCredential googleCredential = null, string projectId = null)
        {
            GcpJsonSecretsConfigurationSource source = new GcpJsonSecretsConfigurationSource(listFilter, googleCredential, projectId);
            return builder.Add(source);
        }

        public static IConfigurationBuilder AddGcpJsonSecrets (this IConfigurationBuilder builder, System.Action<GcpJsonSecretOptions> options) {
            if (null == options) { throw new System.ArgumentNullException(nameof(options)); }
            GcpJsonSecretOptions configOptions = new GcpJsonSecretOptions();
            options(configOptions);
            GcpJsonSecretsConfigurationSource source = new GcpJsonSecretsConfigurationSource(configOptions);
            return builder.Add(source);
        }
    }
}
