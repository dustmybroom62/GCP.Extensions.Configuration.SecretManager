using System.Linq;
using Microsoft.Extensions.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.SecretManager.V1;
using Google.Api.Gax.ResourceNames;

namespace GCP.Extensions.Configuration.SecretManager
{
    public class GcpKeyValueSecretsConfigurationProvider : ConfigurationProvider
    {
        private readonly GoogleCredential _googleCredential;
        private readonly ServiceAccountCredential _serviceAccountCredential;
        public SecretManagerServiceClient SecretMangerClient { get; set; }
        public string SecretNamePrefix { get; set; }
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

        internal string RemovePrefix(string value)
        {
            if (string.IsNullOrEmpty(this.SecretNamePrefix)) { return value; }
            int prefixLen = this.SecretNamePrefix.Length;
            if (prefixLen < (value?.Length ?? 0)) { return value[prefixLen..]; }
            return value;
        }

        public GcpKeyValueSecretsConfigurationProvider(string secretNamePrefix, GoogleCredential googleCredential, string projectId)
        {
            this.SecretNamePrefix = secretNamePrefix;

            _googleCredential = googleCredential ?? GoogleCredential.GetApplicationDefault();
            _serviceAccountCredential = _googleCredential.UnderlyingCredential as ServiceAccountCredential;

            this.ProjectId = projectId ?? _serviceAccountCredential.ProjectId;
            this.SecretMangerClient = BuildClient(_serviceAccountCredential);
        }

        public override void Load()
        {
            ProjectName projectName = new ProjectName(ProjectId);
            string prefix = string.IsNullOrEmpty(this.SecretNamePrefix) ? null : this.SecretNamePrefix.ToLower();
            string filter = (null == prefix) ? string.Empty : $"name:{prefix}";

            ListSecretsRequest request = new ListSecretsRequest() { ParentAsProjectName = projectName, Filter = filter};
            var secrets = this.SecretMangerClient.ListSecrets(request);

            foreach (var secret in secrets)
            {
                if (!((null != prefix) || secret.SecretName.SecretId.ToLower().StartsWith(prefix))) { continue; }

                ListSecretVersionsRequest versionsRequest = new ListSecretVersionsRequest() {
                    Filter = Helpers.FilterVersions_Enabled, ParentAsSecretName = secret.SecretName
                };
                var versions = this.SecretMangerClient.ListSecretVersions(versionsRequest).OrderByDescending(v => v.CreateTime);
                var ver = versions.FirstOrDefault();
                if (null == ver) { continue; }

                var secretVersion = this.SecretMangerClient.AccessSecretVersion(ver.SecretVersionName);
                string trimmedKey = RemovePrefix(secret.SecretName.SecretId);
                string key = ReplacePathSeparator(trimmedKey);
                string value = secretVersion?.Payload.Data.ToStringUtf8();
                Set(key, value);
            }
        }
    }

    public class GcpKeyValueSecretsConfigurationSource : IConfigurationSource
    {
        private readonly GoogleCredential _googleCredential;
        private readonly string _secretNamePrefix;
        private readonly string _projectId;

        public GcpKeyValueSecretsConfigurationSource(string secretNamePrefix = null, GoogleCredential googleCredential = null, string projectId = null)
        {
            _googleCredential = googleCredential;
            _secretNamePrefix = secretNamePrefix;
            _projectId = projectId;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new GcpKeyValueSecretsConfigurationProvider(_secretNamePrefix, _googleCredential, _projectId);
        }
    }

    public static class KeyValueConfigurationExtensions
    {
        public static IConfigurationBuilder AddGcpKeyValueSecrets (this IConfigurationBuilder builder, string secretNamePrefix = null
            , GoogleCredential googleCredential = null, string projectId = null)
        {
            GcpKeyValueSecretsConfigurationSource source = new GcpKeyValueSecretsConfigurationSource(secretNamePrefix, googleCredential, projectId);
            return builder.Add(source);
        }
    }
}
