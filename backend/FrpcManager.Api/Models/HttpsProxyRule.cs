namespace FrpcManager.Api.Models;

public class HttpsProxyRule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int ListenPort { get; set; }
    public string TargetUrl { get; set; } = "";
    public string CertificateMode { get; set; } = "default";
    public string CertificatePath { get; set; } = "";
    public string CertificateKeyPath { get; set; } = "";
    public string CertificatePassword { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsEnabled { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
