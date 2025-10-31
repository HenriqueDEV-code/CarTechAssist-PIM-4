using CarTechAssist.Contracts.Feedback;
using FluentValidation;

namespace CarTechAssist.Application.Validators
{
    public class EnviarFeedbackRequestValidator : AbstractValidator<EnviarFeedbackRequest>
    {
        public EnviarFeedbackRequestValidator()
        {
            RuleFor(x => x.Score)
                .IsInEnum().WithMessage("Score inválido.");

            RuleFor(x => x.Comentario)
                .MaximumLength(1000).WithMessage("Comentário deve ter no máximo 1000 caracteres.")
                .When(x => !string.IsNullOrEmpty(x.Comentario));
        }
    }
}

