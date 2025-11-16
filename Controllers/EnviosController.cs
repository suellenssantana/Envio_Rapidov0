using System.Security.Claims;
using EnvioRapido.Api.DTOs;
using EnvioRapido.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnvioRapido.Api.Controllers;

[ApiController]
[Route("api/envios")]
[Authorize]
public class EnviosController : ControllerBase
{
    private readonly IEnderecoService _enderecoService;
    private readonly IFreteGateway _freteGateway;

    public EnviosController(
        IEnderecoService enderecoService,
        IFreteGateway freteGateway)
    {
        _enderecoService = enderecoService;
        _freteGateway = freteGateway;
    }

    [HttpPost]
    public async Task<IActionResult> CadastrarEnvio([FromBody] EnvioCreateRequest request)
    {
    
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

    
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

       
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


        var frete = await _freteGateway.CalcularAsync(
            remetenteCep: request.RemetenteCep,
            destinatarioCep: request.DestinatarioCep,
            pesoKg: request.PesoKg,
            alturaCm: request.AlturaCm,
            larguraCm: request.LarguraCm,
            comprimentoCm: request.ComprimentoCm);

        if (!frete.ok)
        {
            return BadRequest(new
            {
                message = frete.msg ?? "Não foi possível calcular o frete."
            });
        }

        
        var resposta = new EnvioCadastroResponse(
            RemetenteCep: request.RemetenteCep,
            DestinatarioCep: request.DestinatarioCep,
            PesoKg: request.PesoKg,
            AlturaCm: request.AlturaCm,
            LarguraCm: request.LarguraCm,
            ComprimentoCm: request.ComprimentoCm,
            ValorFrete: frete.valor,
            PrazoDias: frete.prazoDias
        );

        return Ok(resposta);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }
}
