using System.Security.Claims;
using EnvioRapido.Api.DTOs;
using EnvioRapido.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EnvioRapido.Api.Data;
using EnvioRapido.Api.Domain;

namespace EnvioRapido.Api.Controllers;

[ApiController]
[Route("api/envios")]
[Authorize]
public class EnviosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEnderecoService _enderecoService;
    private readonly IFreteGateway _freteGateway;

    public EnviosController(
        AppDbContext db,
        IEnderecoService enderecoService,
        IFreteGateway freteGateway)
    {
        _db = db;
        _enderecoService = enderecoService;
        _freteGateway = freteGateway;
    }

    // POST /api/envios
    [HttpPost]
    public async Task<IActionResult> CadastrarEnvio([FromBody] EnvioCreateRequest request)
    {
        // 1. Validação do modelo
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // 2. Verifica usuário autenticado
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        // 3. Valida CEP remetente
        var (remetenteOk, msgRemetente) =
            await _enderecoService.ValidarCepAsync(request.RemetenteCep);

        if (!remetenteOk)
        {
            return BadRequest(new
            {
                message = msgRemetente ?? "CEP de remetente inválido.",
                campo = "remetenteCep"
            });
        }

        // 4. Valida CEP destinatário
        var (destinatarioOk, msgDestinatario) =
            await _enderecoService.ValidarCepAsync(request.DestinatarioCep);

        if (!destinatarioOk)
        {
            return BadRequest(new
            {
                message = msgDestinatario ?? "CEP de destinatário inválido.",
                campo = "destinatarioCep"
            });
        }

        // 5. Calcula frete via MelhorEnvio
        var frete = await _freteGateway.CalcularAsync(
            remetenteCep: request.RemetenteCep,
            destinatarioCep: request.DestinatarioCep,
            pesoKg: request.PesoKg,
            alturaCm: request.AlturaCm,
            larguraCm: request.LarguraCm,
            comprimentoCm: request.ComprimentoCm
        );

        if (!frete.ok)
        {
            return BadRequest(new
            {
                message = frete.msg ?? "Não foi possível calcular o frete."
            });
        }

        // 6. Salva o envio no banco MySQL
        var envio = new Envio
        {
            UserId = userId,
            RemetenteCep = request.RemetenteCep,
            DestinatarioCep = request.DestinatarioCep,
            PesoKg = request.PesoKg,
            AlturaCm = request.AlturaCm,
            LarguraCm = request.LarguraCm,
            ComprimentoCm = request.ComprimentoCm,
            ValorFrete = frete.valor,
            PrazoDias = frete.prazoDias,
            CriadoEm = DateTime.UtcNow
        };

        _db.Envios.Add(envio);
        await _db.SaveChangesAsync();

        // 7. Retorno idêntico ao PDF
        var resposta = new EnvioCadastroResponse(
            RemetenteCep: envio.RemetenteCep,
            DestinatarioCep: envio.DestinatarioCep,
            PesoKg: envio.PesoKg,
            AlturaCm: envio.AlturaCm,
            LarguraCm: envio.LarguraCm,
            ComprimentoCm: envio.ComprimentoCm,
            ValorFrete: envio.ValorFrete,
            PrazoDias: envio.PrazoDias
        );

        return Ok(resposta);
    }

    // Extrai ID do usuário autenticado
    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }
}
