using CarTechAssist.Contracts.Usuarios;
using FluentValidation;

namespace CarTechAssist.Application.Validators
{
    public class CriarUsuarioRequestValidator : AbstractValidator<CriarUsuarioRequest>
    {
        public CriarUsuarioRequestValidator()
        {
            RuleFor(x => x.Login)
                .NotEmpty().WithMessage("Login é obrigatório.")
                .MaximumLength(100).WithMessage("Login deve ter no máximo 100 caracteres.");

            RuleFor(x => x.NomeCompleto)
                .NotEmpty().WithMessage("Nome completo é obrigatório.")
                .MaximumLength(200).WithMessage("Nome completo deve ter no máximo 200 caracteres.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email é obrigatório para clientes.")
                .EmailAddress().WithMessage("Email inválido.")
                .When(x => x.TipoUsuarioId == 1); // Cliente = 1

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Email inválido.")
                .When(x => x.TipoUsuarioId != 1 && !string.IsNullOrEmpty(x.Email));

            RuleFor(x => x.Senha)
                .NotEmpty().WithMessage("Senha é obrigatória.")
                .MinimumLength(6).WithMessage("Senha deve ter no mínimo 6 caracteres.");

            RuleFor(x => x.TipoUsuarioId)
                .IsInEnum().WithMessage("Tipo de usuário inválido.");

            RuleFor(x => x.Telefone)
                .MaximumLength(20).WithMessage("Telefone deve ter no máximo 20 caracteres.")
                .When(x => !string.IsNullOrEmpty(x.Telefone));
        }
    }

}

