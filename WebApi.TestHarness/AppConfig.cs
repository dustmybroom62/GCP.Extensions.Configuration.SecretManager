
public class EmailConfig
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string From { get; set; }
}

public class AppConfig
{
    public int LocalOnlySetting01 { get; set; }
    public int Setting01 { get; set; }
    public string Setting02 { get; set; }
    public string Key01 { get; set; }
    public string Key02 { get; set; }
    public EmailConfig Email { get; set; } = new();
}