﻿using System.Linq;
using Microsoft.Extensions.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.SecretManager.V1;
using Google.Api.Gax.ResourceNames;

namespace GCP.Extensions.Configuration.SecretManager
{
    public class KeyValueConfigurationProvider : ConfigurationProvider
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

        public KeyValueConfigurationProvider(string listFilter, GoogleCredential googleCredential, string projectId)
        {
            this.ListFilter = listFilter;

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

            foreach (var secret in secrets)
            {
                ListSecretVersionsRequest versionsRequest = new ListSecretVersionsRequest() {
                    Filter = Helpers.FilterVersions_Enabled, ParentAsSecretName = secret.SecretName
                };
                var versions = this.SecretMangerClient.ListSecretVersions(versionsRequest).OrderByDescending(v => v.CreateTime);
                var ver = versions.FirstOrDefault();
                if (null == ver) { continue; }

                var secretVersion = this.SecretMangerClient.AccessSecretVersion(ver.SecretVersionName);
                string key = ReplacePathSeparator(secret.SecretName.SecretId);
                string value = secretVersion?.Payload.Data.ToStringUtf8();
                Set(key, value);
            }
        }
    }

    public class KeyValueConfigurationSource : IConfigurationSource
    {
        private readonly GoogleCredential _googleCredential;
        private readonly string _listFilter;
        private readonly string _projectId;

        public KeyValueConfigurationSource(string listFilter = null, GoogleCredential googleCredential = null, string projectId = null)
        {
            _googleCredential = googleCredential;
            _listFilter = listFilter;
            _projectId = projectId;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new KeyValueConfigurationProvider(_listFilter, _googleCredential, _projectId);
        }
    }

    public static class KeyValueConfigurationExtensions
    {
        public static IConfigurationBuilder AddGcpKeyValueSecrets (this IConfigurationBuilder builder, string listFilter = null
            , GoogleCredential googleCredential = null, string projectId = null)
        {
            KeyValueConfigurationSource source = new KeyValueConfigurationSource(listFilter, googleCredential, projectId);
            return builder.Add(source);
        }
    }
}
