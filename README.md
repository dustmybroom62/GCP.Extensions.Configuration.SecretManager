# GCP.Extensions.Configuration.SecretManager

Configuration provider for Microsoft.Extensions.Configuration framework.

See https://cloud.google.com/secret-manager/docs/reference/libraries on how to create application credential file.

When running outside cloud:
 - Set Environment variable GOOGLE_APPLICATION_CREDENTIALS to enable `GoogleCredential.GetApplicationDefault()`.
 - Build GoogleCredential in code with `GoogleCredential.GetApplicationDefault()`, from file, or from JSON.

Set ProjectId value to over-ride value from GoogleCredential, inside or outside of cloud.

> Update (v3.1.1) ProjectId, when not specified, is auto-populated from:
> 1. ServiceAccountCredential
> 2. The GCP API ( Google.Api.Gax.Platform.Instance().ProjectId )  
> 3. Environment variables ( "GOOGLE_CLOUD_PROJECT", "GCLOUD_PROJECT" ).

### Important! Only the most recently created, ENABLED version of any secret will be used.

---
```
// typical usage:
//   GoogleCredential will be created as GoogleCredential.GetApplicationDefault()
//   ProjectId will be derived from GoogleCredential if ServiceAccountCredential
//   or environment otherwise.
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((hostContext, builder) => {

        // filter rules: https://cloud.google.com/secret-manager/docs/filtering
            builder.AddGcpJsonSecrets("name:servicename_appsettings_");

        // prefix will filter list, then be stripped from key names.
            builder.AddGcpKeyValueSecrets("servicename_keys_");
        })
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        });

```
---
```
// build credential from existing configuration settings:
//   ProjectId will be derived from GoogleCredential if ServiceAccountCredential
//   or environment otherwise.
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((hostContext, builder) => {

        var tempConfig = builder.Build();
        var googleCredential = getGoogleCredentialFromConfig(tempConfig);

        // filter rules: https://cloud.google.com/secret-manager/docs/filtering
            builder.AddGcpJsonSecrets("name:servicename_appsettings_", googleCredential);

        // prefix will filter list, then be stripped from key names.
            builder.AddGcpKeyValueSecrets("servicename_keys_", googleCredential);
        })
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        });

```
