using FrpcManager.Api.Data;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Controllers;

[ApiController]
[Route("api/https-proxies")]
[Authorize]
public class HttpsProxyController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HttpsProxyRuntimeService _runtime;
    private readonly AuditLogService _auditLogService;

    public HttpsProxyController(AppDbContext db, HttpsProxyRuntimeService runtime, AuditLogService auditLogService)
    {
        _db = db;
        _runtime = runtime;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rules = await _db.HttpsProxyRules
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => ToResponse(r))
            .ToListAsync();

        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] HttpsProxyRuleRequest request, IFormFile? certificate, IFormFile? privateKey)
    {
        var validation = await ValidateRequestAsync(request);
        if (validation != null) return validation;

        var rule = new HttpsProxyRule
        {
            Name = request.Name.Trim(),
            ListenPort = request.ListenPort,
            TargetUrl = NormalizeTargetUrl(request.TargetUrl),
            CertificateMode = NormalizeCertificateMode(request.CertificateMode),
            CertificatePassword = request.CertificatePassword ?? "",
            Description = request.Description?.Trim() ?? "",
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow
        };

        if (rule.CertificateMode == "pfx")
        {
            if (certificate == null || certificate.Length == 0)
                return BadRequest(new { message = "请上传 IIS 证书文件（.pfx/.p12）" });

            if (!IsPfx(certificate))
                return BadRequest(new { message = "IIS 证书仅支持 .pfx/.p12 文件" });

            rule.CertificatePath = await SaveCertificateAsync(certificate);
        }
        else if (rule.CertificateMode == "pem")
        {
            if (certificate == null || certificate.Length == 0)
                return BadRequest(new { message = "请上传 Nginx 证书文件（.pem/.crt/.cer）" });

            if (privateKey == null || privateKey.Length == 0)
                return BadRequest(new { message = "请上传 Nginx 私钥文件（.key）" });

            if (!IsPemCertificate(certificate))
                return BadRequest(new { message = "Nginx 证书仅支持 .pem/.crt/.cer 文件" });

            if (!IsPrivateKey(privateKey))
                return BadRequest(new { message = "Nginx 私钥仅支持 .key 文件" });

            rule.CertificatePath = await SaveCertificateAsync(certificate);
            rule.CertificateKeyPath = await SaveCertificateAsync(privateKey);
        }

        _db.HttpsProxyRules.Add(rule);
        await _db.SaveChangesAsync();
        var startError = await TryRestartAsync(rule);
        if (startError != null)
        {
            _db.HttpsProxyRules.Remove(rule);
            await _db.SaveChangesAsync();
            return startError;
        }

        await _auditLogService.LogAsync(HttpContext, "https-proxy.create", rule.Name, $"{rule.ListenPort}->{rule.TargetUrl}");
        return Ok(ToResponse(rule));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromForm] HttpsProxyRuleRequest request, IFormFile? certificate, IFormFile? privateKey)
    {
        var rule = await _db.HttpsProxyRules.FindAsync(id);
        if (rule == null)
            return NotFound(new { message = "HTTPS 代理规则不存在" });

        var validation = await ValidateRequestAsync(request, id);
        if (validation != null) return validation;

        rule.Name = request.Name.Trim();
        rule.ListenPort = request.ListenPort;
        rule.TargetUrl = NormalizeTargetUrl(request.TargetUrl);
        rule.CertificateMode = NormalizeCertificateMode(request.CertificateMode);
        rule.CertificatePassword = request.CertificatePassword ?? rule.CertificatePassword;
        rule.Description = request.Description?.Trim() ?? "";
        rule.IsEnabled = request.IsEnabled;
        rule.UpdatedAt = DateTime.UtcNow;

        if (certificate is { Length: > 0 })
        {
            if (rule.CertificateMode == "pfx" && !IsPfx(certificate))
                return BadRequest(new { message = "IIS 证书仅支持 .pfx/.p12 文件" });

            if (rule.CertificateMode == "pem" && !IsPemCertificate(certificate))
                return BadRequest(new { message = "Nginx 证书仅支持 .pem/.crt/.cer 文件" });

            rule.CertificatePath = await SaveCertificateAsync(certificate);
        }

        if (privateKey is { Length: > 0 })
        {
            if (rule.CertificateMode != "pem")
                return BadRequest(new { message = "只有 Nginx 证书模式需要上传私钥文件" });

            if (!IsPrivateKey(privateKey))
                return BadRequest(new { message = "Nginx 私钥仅支持 .key 文件" });

            rule.CertificateKeyPath = await SaveCertificateAsync(privateKey);
        }

        if (rule.CertificateMode == "pfx" && string.IsNullOrWhiteSpace(rule.CertificatePath))
            return BadRequest(new { message = "请上传 IIS 证书文件（.pfx/.p12）" });

        if (rule.CertificateMode == "pem" &&
            (string.IsNullOrWhiteSpace(rule.CertificatePath) || string.IsNullOrWhiteSpace(rule.CertificateKeyPath)))
            return BadRequest(new { message = "请上传 Nginx 证书文件和私钥文件" });

        await _db.SaveChangesAsync();
        var startError = await TryRestartAsync(rule);
        if (startError != null)
            return startError;

        await _auditLogService.LogAsync(HttpContext, "https-proxy.update", rule.Name, $"{rule.ListenPort}->{rule.TargetUrl}");
        return Ok(ToResponse(rule));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await _db.HttpsProxyRules.FindAsync(id);
        if (rule == null)
            return NotFound(new { message = "HTTPS 代理规则不存在" });

        await _runtime.StopAsync(rule.Id);
        _db.HttpsProxyRules.Remove(rule);
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "https-proxy.delete", rule.Name);
        return Ok(new { message = "HTTPS 代理规则已删除" });
    }

    [HttpPut("{id:int}/enable")]
    public async Task<IActionResult> Enable(int id)
    {
        var rule = await _db.HttpsProxyRules.FindAsync(id);
        if (rule == null)
            return NotFound(new { message = "HTTPS 代理规则不存在" });

        rule.IsEnabled = true;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var startError = await TryRestartAsync(rule);
        if (startError != null)
        {
            rule.IsEnabled = false;
            rule.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return startError;
        }

        await _auditLogService.LogAsync(HttpContext, "https-proxy.enable", rule.Name);
        return Ok(ToResponse(rule));
    }

    [HttpPut("{id:int}/disable")]
    public async Task<IActionResult> Disable(int id)
    {
        var rule = await _db.HttpsProxyRules.FindAsync(id);
        if (rule == null)
            return NotFound(new { message = "HTTPS 代理规则不存在" });

        rule.IsEnabled = false;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _runtime.StopAsync(rule.Id);
        await _auditLogService.LogAsync(HttpContext, "https-proxy.disable", rule.Name);
        return Ok(ToResponse(rule));
    }

    private async Task<BadRequestObjectResult?> ValidateRequestAsync(HttpsProxyRuleRequest request, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "请输入名称" });

        if (request.ListenPort is < 1 or > 65535)
            return BadRequest(new { message = "监听端口必须在 1-65535 之间" });

        if (request.ListenPort is 6887 or 6888)
            return BadRequest(new { message = "监听端口不能使用管理平台端口 6887/6888" });

        if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var targetUri) || targetUri.Scheme != Uri.UriSchemeHttp)
            return BadRequest(new { message = "目标地址必须是 HTTP URL，例如 http://192.168.1.10:8080" });

        var exists = await _db.HttpsProxyRules.AnyAsync(r => r.ListenPort == request.ListenPort && (!currentId.HasValue || r.Id != currentId));
        if (exists)
            return BadRequest(new { message = "监听端口已被其他规则使用" });

        return null;
    }

    private async Task<IActionResult?> TryRestartAsync(HttpsProxyRule rule)
    {
        try
        {
            await _runtime.RestartAsync(rule);
            return null;
        }
        catch (IOException ex)
        {
            await _runtime.StopAsync(rule.Id);
            return BadRequest(new { message = $"HTTPS 代理启动失败，端口可能已被占用：{ex.Message}" });
        }
        catch (InvalidOperationException ex)
        {
            await _runtime.StopAsync(rule.Id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            await _runtime.StopAsync(rule.Id);
            return StatusCode(500, new { message = $"HTTPS 代理启动失败：{ex.Message}" });
        }
    }

    private static string NormalizeTargetUrl(string targetUrl)
    {
        var uri = new Uri(targetUrl.Trim());
        return uri.ToString().TrimEnd('/');
    }

    private static async Task<string> SaveCertificateAsync(IFormFile certificate)
    {
        var certDir = Path.Combine(AppContext.BaseDirectory, "data", "certs");
        Directory.CreateDirectory(certDir);
        var extension = Path.GetExtension(certificate.FileName).ToLowerInvariant();
        var certPath = Path.Combine(certDir, $"{Guid.NewGuid():N}{extension}");
        await using var stream = System.IO.File.Create(certPath);
        await certificate.CopyToAsync(stream);
        return certPath;
    }

    private static bool IsPfx(IFormFile certificate) =>
        Path.GetExtension(certificate.FileName).Equals(".pfx", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(certificate.FileName).Equals(".p12", StringComparison.OrdinalIgnoreCase);

    private static bool IsPemCertificate(IFormFile certificate)
    {
        var extension = Path.GetExtension(certificate.FileName);
        return extension.Equals(".pem", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".crt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivateKey(IFormFile certificate) =>
        Path.GetExtension(certificate.FileName).Equals(".key", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCertificateMode(string? certificateMode)
    {
        return certificateMode switch
        {
            "pfx" => "pfx",
            "pem" => "pem",
            "uploaded" => "pfx",
            _ => "default"
        };
    }

    private static HttpsProxyRuleResponse ToResponse(HttpsProxyRule rule) => new(
        rule.Id,
        rule.Name,
        rule.ListenPort,
        rule.TargetUrl,
        rule.CertificateMode,
        !string.IsNullOrWhiteSpace(rule.CertificatePath),
        !string.IsNullOrWhiteSpace(rule.CertificateKeyPath),
        rule.Description,
        rule.IsEnabled,
        rule.CreatedAt,
        rule.UpdatedAt);
}
