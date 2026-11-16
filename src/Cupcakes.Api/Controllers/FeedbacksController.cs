using Cupcakes.Api.Data;
using Cupcakes.Api.Dtos;
using Cupcakes.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cupcakes.Api.Controllers;

/// <summary>Feedback do cliente sobre produto ou atendimento (RF12).</summary>
[ApiController]
[Authorize]
[Route("api/feedbacks")]
public class FeedbacksController(FeedbackService servico, FeedbackRepositorio feedbacks) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<FeedbackDto>> Enviar(CriarFeedbackRequest req, CancellationToken ct)
    {
        var feedback = await servico.EnviarAsync(User.IdUsuario(), req, ct);
        return Created($"/api/feedbacks/{feedback.IdFeedback}", feedback.ToDto());
    }

    /// <summary>Feedbacks já enviados pelo próprio cliente.</summary>
    [HttpGet]
    public async Task<List<FeedbackDto>> Meus(CancellationToken ct) =>
        (await feedbacks.ListarDoClienteAsync(User.IdUsuario(), ct)).Select(f => f.ToDto()).ToList();
}
