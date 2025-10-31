using CarTechAssist.Contracts.Enums;

namespace CarTechAssist.Contracts.Feedback
{
    public record EnviarFeedbackRequest(
        IAFeedbackScoreDto Score,
        string? Comentario
    );
}

