using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.SecretManager.V1;
using Google.Api.Gax.ResourceNames;

namespace GCP.Extensions.Configuration.SecretManager
{
    public class GcpJsonSecretsConfigurationProvider : JsonStreamConfigurationProvider
    {
        private readonly GoogleCredential _googleCredential;
        private readonly ServiceAccountCredential _serviceAccountCredential;

        public SecretManagerServiceClient SecretMangerClient { get; set; }
        public string ListFilter { get; set; }
        public string ProjectId { get; set; }

        internal SecretManagerServiceClient BuildClient(ServiceAccountCredential credential) {
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

        public GcpJsonSecretsConfigurationProvider(GcpJsonSecretsConfigurationSource source, string secretListFilter, GoogleCredential googleCredential, string projectId) : base(source)
        {
            this.ListFilter = secretListFilter;

            _googleCredential = googleCredential ?? GoogleCredential.GetApplicationDefault();
            _serviceAccountCredential = _googleCredential.UnderlyingCredential as ServiceAccountCredential;

            this.ProjectId = projectId ?? _serviceAccountCredential.ProjectId;
            this.SecretMangerClient = BuildClient(_serviceAccountCredential);
        }

        public override void Load()
        {
            ProjectName projectName = new ProjectName(ProjectId);
            ListSecretsRequest request = new ListSecretsRequest() { ParentAsProjectName = projectName, Filter = (this.ListFilter ?? string.Empty) };
            var secrets = this.SecretMangerClient.ListSecrets(request);

            var secret = secrets.FirstOrDefault();
            if (null == secret) { return; }

            ListSecretVersionsRequest versionsRequest = new ListSecretVersionsRequest() {
                Filter = Helpers.FilterVersions_Enabled, ParentAsSecretName = secret.SecretName
            };
            var versions = this.SecretMangerClient.ListSecretVersions(versionsRequest).OrderByDescending(v => v.CreateTime);
            var ver = versions.FirstOrDefault();
            if (null == ver) { return; }

            var secretVersion = this.SecretMangerClient.AccessSecretVersion(ver.SecretVersionName);
            System.IO.Stream stream = new System.IO.MemoryStream(secretVersion?.Payload.Data.ToByteArray());
            base.Load(stream);
        }
    }

    public class GcpJsonSecretsConfigurationSource : JsonStreamConfigurationSource
    {
        private readonly GoogleCredential _googleCredential;
        private readonly string _listFilter;
        private readonly string _projectId;

        public GcpJsonSecretsConfigurationSource(string listFilter = null, GoogleCredential googleCredential = null, string projectId = null)
        {
            _googleCredential = googleCredential;
            _listFilter = listFilter;
            _projectId = projectId;
        }

        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new GcpJsonSecretsConfigurationProvider(this, _listFilter, _googleCredential, _projectId);
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
    }
}
