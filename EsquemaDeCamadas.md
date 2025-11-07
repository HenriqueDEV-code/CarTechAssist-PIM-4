# File Tree: CarTechAssist

**Generated:** 11/6/2025, 2:56:19 PM
**Root Path:** `c:\Users\Luis Henrique\OneDrive\Documentos\Visual Studio 2022\Templates\Codigo C#\Projeto PIM Ultimo Semestre\CarTechAssist`

```
├── 📁 .github
│   └── 📁 workflows
├── 📁 CarTechAssist.Api
│   ├── 📁 Attributes
│   │   └── 📄 AuthorizeRolesAttribute.cs
│   ├── 📁 Controllers
│   │   ├── 📄 AuthController.cs
│   │   ├── 📄 CategoriasController.cs
│   │   ├── 📄 ChamadosController.cs
│   │   ├── 📄 EmailTestController.cs
│   │   ├── 📄 RecuperacaoSenhaController.cs
│   │   ├── 📄 SetupController.cs
│   │   ├── 📄 UsuariosController.cs
│   │   └── 📄 WeatherForecastController.cs
│   ├── 📁 Filters
│   │   └── 📄 SwaggerHeaderFilter.cs
│   ├── 📁 Hubs
│   │   └── 📄 ChamadoHub.cs
│   ├── 📁 Middleware
│   │   ├── 📄 GlobalExceptionHandlerMiddleware.cs
│   │   └── 📄 TenantMiddleware.cs
│   ├── 📁 Properties
│   │   └── ⚙️ launchSettings.json
│   ├── 📁 Services
│   │   └── 📄 RefreshTokenCleanupService.cs
│   ├── ⚙️ .gitignore
│   ├── 📄 CarTechAssist.Api.csproj
│   ├── 📄 CarTechAssist.Api.http
│   ├── 📄 Program.cs
│   ├── 📝 README_CONFIGURACAO.md
│   ├── 📄 WeatherForecast.cs
│   ├── 📄 appsettings.Development.json.example
│   └── 📄 appsettings.json.example
├── 📁 CarTechAssist.Application
│   ├── 📁 Mappings
│   │   └── 📄 FeedbackProfile.cs
│   ├── 📁 Services
│   │   ├── 📄 AuthService.cs
│   │   ├── 📄 CategoriasService.cs
│   │   ├── 📄 ChamadosService.cs
│   │   ├── 📄 DialogflowService.cs
│   │   ├── 📄 EmailService.cs
│   │   ├── 📄 EnumHelperService.cs
│   │   ├── 📄 InputSanitizer.cs
│   │   ├── 📄 OpenRouterService.cs
│   │   ├── 📄 RecuperacaoSenhaService.cs
│   │   └── 📄 UsuariosService.cs
│   ├── 📁 Validators
│   │   ├── 📄 CriarChamadoRequestValidator.cs
│   │   ├── 📄 CriarUsuarioRequestValidator.cs
│   │   ├── 📄 EnviarFeedbackRequestValidator.cs
│   │   └── 📄 LoginRequestValidator.cs
│   └── 📄 CarTechAssist.Application.csproj
├── 📁 CarTechAssist.Contracts
│   ├── 📁 Auth
│   │   ├── 📄 LoginRequest.cs
│   │   ├── 📄 LoginResponse.cs
│   │   ├── 📄 RedefinirSenhaRequest.cs
│   │   ├── 📄 RefreshTokenRequest.cs
│   │   ├── 📄 SolicitarRecuperacaoRequest.cs
│   │   └── 📄 UsuarioLogadoDto.cs
│   ├── 📁 Common
│   │   └── 📄 PagedResult.cs
│   ├── 📁 Enums
│   │   ├── ⚙️ .keep
│   │   ├── 📄 IAFeedbackScoreDto.cs
│   │   └── 📄 TicketView.cs
│   ├── 📁 Feedback
│   │   ├── 📄 EnviarFeedbackRequest.cs
│   │   └── 📄 FeedbackRequest.cs
│   ├── 📁 Tickets
│   │   ├── 📄 AdicionarInteracaoRequest.cs
│   │   ├── 📄 AlterarStatusRequest.cs
│   │   ├── 📄 AnexoDto.cs
│   │   ├── 📄 AtribuirResponsavelRequest.cs
│   │   ├── 📄 AtualizarChamadoRequest.cs
│   │   ├── 📄 CategoriaDto.cs
│   │   ├── 📄 ChamadoDetailDto.cs
│   │   ├── 📄 CriarChamadoRequest.cs
│   │   ├── 📄 DeletarChamadoRequest.cs
│   │   ├── 📄 EstatisticasChamadosDto.cs
│   │   ├── 📄 InteracaoDto.cs
│   │   └── 📄 StatusHistoricoDto.cs
│   ├── 📁 Usuarios
│   │   ├── 📄 AlterarAtivacaoRequest.cs
│   │   ├── 📄 AtualizarUsuarioRequest.cs
│   │   ├── 📄 CriarUsuarioRequest.cs
│   │   ├── 📄 ResetSenhaRequest.cs
│   │   └── 📄 UsuarioDto.cs
│   └── 📄 CarTechAssist.Contracts.csproj
├── 📁 CarTechAssist.Desktop.WinForms
│   ├── 📄 CarTechAssist.Desktop.WinForms.csproj
│   ├── 📄 Form1.Designer.cs
│   ├── 📄 Form1.cs
│   └── 📄 Program.cs
├── 📁 CarTechAssist.Domain
│   ├── 📁 Entities
│   │   ├── 📄 ApiClient.cs
│   │   ├── 📄 Auditoria.cs
│   │   ├── 📄 CategoriaChamado.cs
│   │   ├── 📄 Chamado.cs
│   │   ├── 📄 ChamadoAnexo.cs
│   │   ├── 📄 ChamadoInteracao.cs
│   │   ├── 📄 ChamadoStatusHistorico.cs
│   │   ├── 📄 IAFeedback.cs
│   │   ├── 📄 IARunLog.cs
│   │   ├── 📄 RecuperacaoSenha.cs
│   │   ├── 📄 RefreshToken.cs
│   │   ├── 📄 Tenant.cs
│   │   └── 📄 Usuario.cs
│   ├── 📁 Enums
│   │   ├── 📄 CanalAtendimento.cs
│   │   ├── 📄 IAFeedbackScore.cs
│   │   ├── 📄 PrioridadeChamado.cs
│   │   ├── 📄 StatusChamado.cs
│   │   └── 📄 TipoUsuarios.cs
│   ├── 📁 Interfaces
│   │   ├── 📄 IAditoriaRepository.cs
│   │   ├── 📄 IAiProvider.cs
│   │   ├── 📄 IAnexosReposity.cs
│   │   ├── 📄 ICategoriasRepository.cs
│   │   ├── 📄 IChamadosRepository.cs
│   │   ├── 📄 IFeedbackRepository.cs
│   │   ├── 📄 IRecuperacaoSenhaRepository.cs
│   │   ├── 📄 IRefreshTokenRepository.cs
│   │   └── 📄 IUsuariosRepository.cs
│   └── 📄 CarTechAssist.Domain.csproj
├── 📁 CarTechAssist.Infrastruture
│   ├── 📁 Repositories
│   │   ├── 📄 AnexosRepository.cs
│   │   ├── 📄 CategoriasRepository.cs
│   │   ├── 📄 ChamadosRepository.cs
│   │   ├── 📄 FeedbackRepository.cs
│   │   ├── 📄 RecuperacaoSenhaRepository.cs
│   │   ├── 📄 RefreshTokenRepository.cs
│   │   └── 📄 UsuariosRepository.cs
│   └── 📄 CarTechAssist.Infrastruture.csproj
├── 📁 CarTechAssist.Web
│   ├── 📁 Pages
│   │   ├── 📁 Chamados
│   │   │   ├── 📄 Criar.cshtml
│   │   │   ├── 📄 Criar.cshtml.cs
│   │   │   ├── 📄 Detalhes.cshtml
│   │   │   └── 📄 Detalhes.cshtml.cs
│   │   ├── 📁 Shared
│   │   │   ├── 📄 _Layout.cshtml
│   │   │   ├── 🎨 _Layout.cshtml.css
│   │   │   └── 📄 _ValidationScriptsPartial.cshtml
│   │   ├── 📄 Chamados.cshtml
│   │   ├── 📄 Chamados.cshtml.cs
│   │   ├── 📄 Dashboard.cshtml
│   │   ├── 📄 Dashboard.cshtml.cs
│   │   ├── 📄 Error.cshtml
│   │   ├── 📄 Error.cshtml.cs
│   │   ├── 📄 ForgotPassword.cshtml
│   │   ├── 📄 ForgotPassword.cshtml.cs
│   │   ├── 📄 Index.cshtml
│   │   ├── 📄 Index.cshtml.cs
│   │   ├── 📄 Login.cshtml
│   │   ├── 📄 Login.cshtml.cs
│   │   ├── 📄 Privacy.cshtml
│   │   ├── 📄 Privacy.cshtml.cs
│   │   ├── 📄 Register.cshtml
│   │   ├── 📄 Register.cshtml.cs
│   │   ├── 📄 ResetPassword.cshtml
│   │   ├── 📄 ResetPassword.cshtml.cs
│   │   ├── 📄 Usuarios.cshtml
│   │   ├── 📄 Usuarios.cshtml.cs
│   │   ├── 📄 _ViewImports.cshtml
│   │   └── 📄 _ViewStart.cshtml
│   ├── 📁 Properties
│   │   └── ⚙️ launchSettings.json
│   ├── 📁 Services
│   │   ├── 📄 ApiClientService.cs
│   │   ├── 📄 AuthService.cs
│   │   ├── 📄 CategoriasService.cs
│   │   ├── 📄 ChamadosService.cs
│   │   └── 📄 UsuariosService.cs
│   ├── 📁 wwwroot
│   │   ├── 📁 css
│   │   │   └── 🎨 site.css
│   │   ├── 📁 img
│   │   │   ├── 📄 LOGO.ico
│   │   │   └── 🖼️ imgPerfil.png
│   │   ├── 📁 js
│   │   │   └── 📄 site.js
│   │   ├── 📁 lib
│   │   │   ├── 📁 bootstrap
│   │   │   │   └── 📄 LICENSE
│   │   │   ├── 📁 jquery
│   │   │   │   └── 📄 LICENSE.txt
│   │   │   ├── 📁 jquery-validation
│   │   │   │   └── 📝 LICENSE.md
│   │   │   └── 📁 jquery-validation-unobtrusive
│   │   │       ├── 📄 LICENSE.txt
│   │   │       └── 📄 jquery.validate.unobtrusive.js
│   │   └── 📄 favicon.ico
│   ├── 📄 CarTechAssist.Web.csproj
│   └── 📄 Program.cs
├── ⚙️ .gitattributes
├── ⚙️ .gitignore
├── 📄 CarTechAssist.sln
└── 📄 LICENSE.txt
```

---
*Generated by FileTree Pro Extension*