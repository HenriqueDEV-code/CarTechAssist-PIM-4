using CarTechAssist.Contracts.Tickets;
using FluentValidation;

namespace CarTechAssist.Application.Validators
{
    public class CriarChamadoRequestValidator : AbstractValidator<CriarChamadoRequest>
    {
        public CriarChamadoRequestValidator()
        {
            RuleFor(x => x.Titulo)
                .NotEmpty().WithMessage("Título é obrigatório.")
                .MaximumLength(200).WithMessage("Título deve ter no máximo 200 caracteres.");

            RuleFor(x => x.Descricao)
                .NotEmpty().WithMessage("Descrição é obrigatória.")
                .MaximumLength(10000).WithMessage("Descrição deve ter no máximo 10000 caracteres.");

            RuleFor(x => x.CategoriaId)
                .NotNull().WithMessage("Categoria é obrigatória.")
                .GreaterThan(0).WithMessage("CategoriaId deve ser maior que zero.");

            RuleFor(x => x.PrioridadeId)
                .InclusiveBetween((byte)1, (byte)4).WithMessage("PrioridadeId deve estar entre 1 e 4.");

            RuleFor(x => x.CanalId)
                .InclusiveBetween((byte)1, (byte)6).WithMessage("CanalId deve estar entre 1 e 6.");

            RuleFor(x => x.SolicitanteUsuarioId)
                .GreaterThan(0).WithMessage("SolicitanteUsuarioId deve ser maior que zero.");
        }
    }
}

