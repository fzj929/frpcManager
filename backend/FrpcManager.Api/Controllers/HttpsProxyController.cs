using System.Text.RegularExpressions;
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
    private readonly ProxyService _proxyService;
    private readonly AuditLogService _auditLogService;
    private readonly UserContextService _userContext;

    public HttpsProxyController(
        AppDbContext db,
        HttpsProxyRuntimeService runtime,
        ProxyService proxyService,
        AuditLogService auditLogService,
        UserContextService userContext)
    {
        _db = db;
        _runtime = runtime;
        _proxyService = proxyService;
        _auditLogService = auditLogService;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rules = await _db.HttpsProxyRules
            .Include(r => r.CreatedByUser)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(rules.Select(r => ToResponse(r, _userContext.UserId, _userContext.IsAdmin)));
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
            CreatedByUserId = _userContext.UserId,
            CreatedAt = DateTime.UtcNow
        };

        var certValidation = await ApplyUploadedCertificatesAsync(rule, certificate, privateKey, isCreate: true);
        if (certValidation != null) return certValidation;

        _db.HttpsProxyRules.Add(rule);
        await _db.SaveChangesAsync();

        ProxyResponse? createdFrpChannel = null;
        if (request.CreateFrpChannel)
        {
            createdFrpChannel = await _proxyService.CreateProxyAsync(new CreateProxyRequest(
                    request.FrpChannelName!.Trim(),
                    "tcp",
                    "127.0.0.1",
                    rule.ListenPort,
                    rule.ListenPort,
                    rule.Name),
                _userContext.UserId);
        }

        var startError = await TryRestartAsync(rule);
        if (startError != null)
        {
            if (createdFrpChannel != null)
                await _proxyService.DeleteProxyAsync(createdFrpChannel.Id, _userContext.UserId, _userContext.IsAdmin);

            _db.HttpsProxyRules.Remove(rule);
            await _db.SaveChangesAsync();
            return startError;
        }

        await _db.Entry(rule).Reference(r => r.CreatedByUser).LoadAsync();
        await _auditLogService.LogAsync(HttpContext, "https-proxy.create", rule.Name, $"{rule.ListenPort}->{rule.TargetUrl}");
        if (createdFrpChannel != null)
            await _auditLogService.LogAsync(HttpContext, "proxy.create", createdFrpChannel.Name, $"tcp:127.0.0.1:{rule.ListenPort}->{rule.ListenPort}");

        return Ok(ToResponse(rule, _userContext.UserId, _userContext.IsAdmin));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromForm] HttpsProxyRuleRequest request, IFormFile? certificate, IFormFile? privateKey)
    {
        var rule = await _db.HttpsProxyRules
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (rule == null)
            return NotFound(new { message = "HTTPS 代理规则不存在" });
        if (!CanManage(rule))
            return BadRequest(new { message = "没有权限修改这个 HTTPS 代理" });

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

        var certValidation = await ApplyUploadedCertificatesAsync(rule, certificate, privateKey, isCreate: false);
        if (certValidation != null) return certValidation;

        var certificateStateValidation = ValidateCertificateState(rule);
        if (certificateStateValidation != null) return certificateStateValidation;

        await _db.SaveChangesAsync();
        var startError = await TryRestartAsync(rule);
        if (startError != null)
            return startError;

        await _auditLogService.LogAsync(HttpContext, "https-proxy.update", rule.Name, $"{rule.ListenPort}->{rule.TargetUrl}");
        return Ok(ToResponse(rule, _userContext.UserId, _userContext.IsAdmin));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await _db.HttpsProxyRules.FindAsync(id);
        if (rule == null)
            return NotFound(new { message = "HTTPS 代理规则不存在" });
        if (!CanManage(rule))
            return BadRequest(new { message = "没有权限删除这个 HTTPS 代理" });

        await _runtime.StopAsync(rule.Id);
        _db.HttpsProxyRules.Remove(rule);
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "https-proxy.delete", rule.Name);
        return Ok(new { message = "HTTPS 代理规则已删除" });
    }

    [HttpPut("{id:int}/enable")]
    public async Task<IActionResult> Enable(int id)
    {
        var rule = await _db.HttpsProxyRules
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (rule == null)
            return NotFound(new { message = "HTTPS 代理规则不存在" });
        if (!CanManage(rule))
            return BadRequest(new { message = "没有权限操作这个 HTTPS 代理" });

        var conflict = await FindEnabledListenPortConflictAsync(rule.ListenPort, rule.Id);
        if (conflict != null)
            return BadRequest(new { message = $"监听端口 {rule.ListenPort} 已被已启用 HTTPS 代理“{conflict.Name}”使用，不能同时启用" });

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
        return Ok(ToResponse(rule, _userContext.UserId, _userContext.IsAdmin));
    }

    [HttpPut("{id:int}/disable")]
    public async Task<IActionResult> Disable(int id)
    {
        var rule = await _db.HttpsProxyRules
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (rule == null)
            return NotFound(new { message = "HTTPS 代理规则不存在" });
        if (!CanManage(rule))
            return BadRequest(new { message = "没有权限操作这个 HTTPS 代理" });

        rule.IsEnabled = false;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _runtime.StopAsync(rule.Id);
        await _auditLogService.LogAsync(HttpContext, "https-proxy.disable", rule.Name);
        return Ok(ToResponse(rule, _userContext.UserId, _userContext.IsAdmin));
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

        if (request.IsEnabled)
        {
            var conflict = await FindEnabledListenPortConflictAsync(request.ListenPort, currentId);
            if (conflict != null)
                return BadRequest(new { message = $"监听端口 {request.ListenPort} 已被已启用 HTTPS 代理“{conflict.Name}”使用，不能同时启用" });
        }

        if (!currentId.HasValue && request.CreateFrpChannel)
        {
            if (string.IsNullOrWhiteSpace(request.FrpChannelName))
                return BadRequest(new { message = "请输入 frp 通道名称" });

            var channelName = request.FrpChannelName.Trim();
            if (channelName.Length > 64)
                return BadRequest(new { message = "frp 通道名称不能超过 64 个字符" });

            if (!Regex.IsMatch(channelName, "^[a-zA-Z0-9_-]+$"))
                return BadRequest(new { message = "frp 通道名称只能包含字母、数字、下划线和连字符" });

            var channelExists = await _db.Proxies.AnyAsync(p =>
                p.Type == "tcp" && p.Name.ToLower() == channelName.ToLower());
            if (channelExists)
                return BadRequest(new { message = "frp 通道名称已存在" });
        }

        return null;
    }

    private async Task<BadRequestObjectResult?> ApplyUploadedCertificatesAsync(
        HttpsProxyRule rule,
        IFormFile? certificate,
        IFormFile? privateKey,
        bool isCreate)
    {
        if (rule.CertificateMode == "pfx")
        {
            if (isCreate && (certificate == null || certificate.Length == 0))
                return BadRequest(new { message = "请上传 IIS 证书文件（.pfx/.p12）" });

            if (certificate is { Length: > 0 })
            {
                if (!IsPfx(certificate))
                    return BadRequest(new { message = "IIS 证书仅支持 .pfx/.p12 文件" });

                rule.CertificatePath = await SaveCertificateAsync(certificate);
            }
        }
        else if (rule.CertificateMode == "pem")
        {
            if (isCreate && (certificate == null || certificate.Length == 0))
                return BadRequest(new { message = "请上传 Nginx 证书文件（.pem/.crt/.cer）" });

            if (isCreate && (privateKey == null || privateKey.Length == 0))
                return BadRequest(new { message = "请上传 Nginx 私钥文件（.key）" });

            if (certificate is { Length: > 0 })
            {
                if (!IsPemCertificate(certificate))
                    return BadRequest(new { message = "Nginx 证书仅支持 .pem/.crt/.cer 文件" });

                rule.CertificatePath = await SaveCertificateAsync(certificate);
            }

            if (privateKey is { Length: > 0 })
            {
                if (!IsPrivateKey(privateKey))
                    return BadRequest(new { message = "Nginx 私钥仅支持 .key 文件" });

                rule.CertificateKeyPath = await SaveCertificateAsync(privateKey);
            }
        }
        else if (privateKey is { Length: > 0 })
        {
            return BadRequest(new { message = "只有 Nginx 证书模式需要上传私钥文件" });
        }

        return null;
    }

    private BadRequestObjectResult? ValidateCertificateState(HttpsProxyRule rule)
    {
        if (rule.CertificateMode == "pfx" && string.IsNullOrWhiteSpace(rule.CertificatePath))
            return BadRequest(new { message = "请上传 IIS 证书文件（.pfx/.p12）" });

        if (rule.CertificateMode == "pem" &&
            (string.IsNullOrWhiteSpace(rule.CertificatePath) || string.IsNullOrWhiteSpace(rule.CertificateKeyPath)))
            return BadRequest(new { message = "请上传 Nginx 证书文件和私钥文件" });

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

    private async Task<HttpsProxyRule?> FindEnabledListenPortConflictAsync(int listenPort, int? currentId)
    {
        return await _db.HttpsProxyRules
            .Where(r =>
                r.IsEnabled &&
                r.ListenPort == listenPort &&
                (!currentId.HasValue || r.Id != currentId.Value))
            .OrderBy(r => r.Name)
            .FirstOrDefaultAsync();
    }

    private bool CanManage(HttpsProxyRule rule) =>
        _userContext.IsAdmin || (_userContext.UserId.HasValue && rule.CreatedByUserId == _userContext.UserId.Value);

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

    private static HttpsProxyRuleResponse ToResponse(HttpsProxyRule rule, int? currentUserId, bool isAdmin) => new(
        rule.Id,
        rule.Name,
        rule.ListenPort,
        rule.TargetUrl,
        rule.CertificateMode,
        !string.IsNullOrWhiteSpace(rule.CertificatePath),
        !string.IsNullOrWhiteSpace(rule.CertificateKeyPath),
        rule.Description,
        rule.IsEnabled,
        rule.CreatedByUserId,
        rule.CreatedByUser?.Username ?? "",
        isAdmin || (currentUserId.HasValue && rule.CreatedByUserId == currentUserId.Value),
        rule.CreatedAt,
        rule.UpdatedAt);
}
