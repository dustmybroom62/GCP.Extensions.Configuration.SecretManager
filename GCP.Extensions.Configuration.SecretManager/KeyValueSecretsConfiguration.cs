using System.Linq;
using Microsoft.Extensions.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.SecretManager.V1;
using Google.Api.Gax.ResourceNames;

namespace GCP.Extensions.Configuration.SecretManager
{
    /// <summary>Options for the configuration of the GcpKeyValueSecretsConfigurationProvider</summary>
    public class GcpKeyValueSecretOptions {
    /// <summary>(optinal) the GoogleCredential to use.</summary>
        public GoogleCredential GoogleCredential {get;set;}
    /// <summary>(optional) the SecretManagerServiceClient to use. if specified GoogleCredential is ignored.</summary>
        public SecretManagerServiceClient SecretMangerClient { get; set; }
    /// <summary>(optional) a prefix to use in filtering the list of secret names.</summary>
        public string SecretNamePrefix { get; set; }
    /// <summary>(optional) the ProjectId. if ommited, ProjectId will be dirived from Credential or Environment</summary>
        public string ProjectId { get; set; }
    /// <summary>(default: true) whether to keep or remove the prefix from the configuration key name.</summary>
        public bool StripPrefixFromKey {get;set;} = true;
    }

    public class GcpKeyValueSecretsConfigurationProvider : ConfigurationProvider
    {
        private GcpKeyValueSecretOptions _options;

        internal SecretManagerServiceClient BuildClient(ICredential credential)
        {
            if (null == credential)
            {
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

        internal string RemovePrefix(string value)
        {
            if (string.IsNullOrEmpty(_options.SecretNamePrefix)) { return value; }
            if (_options.StripPrefixFromKey) {
                int prefixLen = _options.SecretNamePrefix.Length;
                if (prefixLen < (value?.Length ?? 0)) { return value[prefixLen..]; }
            }
            return value;
        }

        public GcpKeyValueSecretsConfigurationProvider(GcpKeyValueSecretOptions options) {
            if (null == options) { throw new System.ArgumentNullException(nameof(options)); }
            _options = options;
            if (null == _options.SecretMangerClient) {
                _options.GoogleCredential ??= GoogleCredential.GetApplicationDefault();
                _options.SecretMangerClient = BuildClient(_options.GoogleCredential.UnderlyingCredential);
            }
        }

        public override void Load()
        {
            ProjectName projectName = new ProjectName(_options.ProjectId);
            string prefix = string.IsNullOrEmpty(_options.SecretNamePrefix) ? null : _options.SecretNamePrefix.ToLower();
            string filter = (null == prefix) ? string.Empty : $"name:{prefix}";

            ListSecretsRequest request = new ListSecretsRequest() { ParentAsProjectName = projectName, Filter = filter};
            var secrets = _options.SecretMangerClient.ListSecrets(request);

            foreach (var secret in secrets)
            {
                if (!((null != prefix) || secret.SecretName.SecretId.ToLower().StartsWith(prefix))) { continue; }

                ListSecretVersionsRequest versionsRequest = new ListSecretVersionsRequest() {
                    Filter = Helpers.FilterVersions_Enabled, ParentAsSecretName = secret.SecretName
                };
                var versions = _options.SecretMangerClient.ListSecretVersions(versionsRequest).OrderByDescending(v => v.CreateTime);
                var ver = versions.FirstOrDefault();
                if (null == ver) { continue; }

                var secretVersion = _options.SecretMangerClient.AccessSecretVersion(ver.SecretVersionName);
                string trimmedKey = RemovePrefix(secret.SecretName.SecretId);
                string key = ReplacePathSeparator(trimmedKey);
                string value = secretVersion?.Payload.Data.ToStringUtf8();
                Set(key, value);
            }
        }
    }

    public class GcpKeyValueSecretsConfigurationSource : IConfigurationSource
    {
        private GcpKeyValueSecretOptions _options;

        public GcpKeyValueSecretsConfigurationSource(string secretNamePrefix = null, GoogleCredential googleCredential = null, string projectId = null)
        {
            _options = new GcpKeyValueSecretOptions();
            _options.GoogleCredential = googleCredential;
            _options.SecretNamePrefix = secretNamePrefix;
            _options.ProjectId = projectId;
        }

        public GcpKeyValueSecretsConfigurationSource(GcpKeyValueSecretOptions options) {
            if (null == options) { throw new System.ArgumentNullException(nameof(options)); }
            _options = options;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new GcpKeyValueSecretsConfigurationProvider(_options);
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

        public static IConfigurationBuilder AddGcpKeyValueSecrets (this IConfigurationBuilder builder, System.Action<GcpKeyValueSecretOptions> options) {
            if (null == options) { throw new System.ArgumentNullException(nameof(options)); }
            GcpKeyValueSecretOptions configOptions = new GcpKeyValueSecretOptions();
            options(configOptions);
            GcpKeyValueSecretsConfigurationSource source = new GcpKeyValueSecretsConfigurationSource(configOptions);
            return builder.Add(source);
        }
    }
}
