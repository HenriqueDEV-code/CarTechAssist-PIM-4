# 🚗 CarTechAssist

Sistema completo de gerenciamento de chamados técnicos (tickets) multi-tenant com suporte a IA, desenvolvido em .NET 8.0.

## 📋 Índice

- [Sobre o Projeto](#sobre-o-projeto)
- [Arquitetura](#arquitetura)
- [Tecnologias](#tecnologias)
- [Funcionalidades](#funcionalidades)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Pré-requisitos](#pré-requisitos)
- [Configuração](#configuração)
- [Executando o Projeto](#executando-o-projeto)
- [API](#api)
- [Documentação](#documentação)

## 🎯 Sobre o Projeto

O **CarTechAssist** é uma solução completa para gerenciamento de chamados técnicos que oferece:

- **Multi-tenant**: Suporte a múltiplos clientes/tenants isolados
- **IA Integrada**: OpenRouter (gateway unificado para múltiplos modelos de IA) e Dialogflow para categorização automática
- **Tempo Real**: Comunicação em tempo real via SignalR
- **Múltiplas Interfaces**: API REST, aplicação web e desktop
- **Segurança**: Autenticação JWT, rate limiting e sanitização de inputs

## 🏗️ Arquitetura

O projeto segue uma **arquitetura em camadas (Clean Architecture)** com separação clara de responsabilidades:

```
┌─────────────────────────────────────┐
│   CarTechAssist.Web (Razor Pages)  │
│   CarTechAssist.Desktop.WinForms   │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│   CarTechAssist.Api (REST API)      │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│   CarTechAssist.Application          │
│   (Services, Validators, Mappings)   │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│   CarTechAssist.Domain               │
│   (Entities, Enums, Interfaces)      │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│   CarTechAssist.Infrastructure       │
│   (Repositories, Data Access)        │
└──────────────────────────────────────┘
```

### Camadas

- **Domain**: Entidades, enums e interfaces do domínio
- **Contracts**: DTOs e contratos de comunicação
- **Application**: Lógica de negócio, serviços, validações e mapeamentos
- **Infrastructure**: Implementação de repositórios e acesso a dados
- **Api**: Controladores REST, middleware e configurações da API
- **Web**: Interface web com Razor Pages
- **Desktop.WinForms**: Aplicação desktop (em desenvolvimento)

## 🛠️ Tecnologias

### Backend
- **.NET 8.0**
- **ASP.NET Core** (API e Web)
- **Entity Framework Core** (implícito via repositórios)
- **SQL Server**
- **SignalR** (comunicação em tempo real)
- **JWT Authentication**
- **FluentValidation**
- **AutoMapper**

### IA e Integrações
- **OpenRouter API** (Gateway unificado para múltiplos modelos de IA - OpenAI, Anthropic, etc.)
- **Google Dialogflow** (Opcional, como fallback)
- **HtmlSanitizer** (sanitização de inputs)

### Frontend
- **Razor Pages** (ASP.NET Core)
- **Bootstrap**
- **jQuery**
- **SignalR Client**

### Segurança e Performance
- **AspNetCoreRateLimit** (rate limiting)
- **Response Compression** (Brotli/Gzip)
- **Health Checks**

## ✨ Funcionalidades

### Gestão de Chamados
- ✅ Criação, edição e exclusão de chamados
- ✅ Categorização automática por IA
- ✅ Atribuição de responsáveis
- ✅ Controle de status e prioridades
- ✅ Histórico de alterações
- ✅ Anexos de arquivos
- ✅ SLA e prazos estimados
- ✅ Estatísticas e relatórios

### Sistema de Usuários
- ✅ Autenticação JWT
- ✅ Recuperação de senha
- ✅ Gestão de usuários (CRUD)
- ✅ Controle de ativação/desativação
- ✅ Diferentes tipos de usuários (roles)

### IA e Categorização Automática
- ✅ Integração com OpenRouter (suporte a múltiplos modelos de IA)
- ✅ Categorização automática de chamados
- ✅ Sugestão de prioridades
- ✅ Resumo automático de chamados
- ✅ Feedback de qualidade da IA

### Multi-tenant
- ✅ Isolamento completo de dados por tenant
- ✅ Middleware de tenant automático
- ✅ Validação de tenant em todas as requisições

### Segurança
- ✅ Autenticação JWT com refresh tokens
- ✅ Rate limiting por endpoint
- ✅ Sanitização de inputs
- ✅ CORS configurável
- ✅ Validação de dados com FluentValidation

## 📁 Estrutura do Projeto

```
CarTechAssist/
├── CarTechAssist.Api/              # API REST
│   ├── Controllers/                 # Controladores da API
│   ├── Hubs/                        # SignalR Hubs
│   ├── Middleware/                  # Middlewares customizados
│   ├── Services/                   # Serviços da API
│   └── Filters/                     # Filtros do Swagger
│
├── CarTechAssist.Application/       # Camada de Aplicação
│   ├── Services/                    # Serviços de negócio
│   ├── Validators/                  # Validações FluentValidation
│   └── Mappings/                    # Mapeamentos AutoMapper
│
├── CarTechAssist.Contracts/         # DTOs e Contratos
│   ├── Auth/                        # Contratos de autenticação
│   ├── Tickets/                     # Contratos de chamados
│   ├── Usuarios/                    # Contratos de usuários
│   └── Feedback/                    # Contratos de feedback
│
├── CarTechAssist.Domain/            # Camada de Domínio
│   ├── Entities/                    # Entidades do domínio
│   ├── Enums/                       # Enumerações
│   └── Interfaces/                  # Interfaces dos repositórios
│
├── CarTechAssist.Infrastructure/    # Camada de Infraestrutura
│   └── Repositories/                # Implementação dos repositórios
│
├── CarTechAssist.Web/               # Aplicação Web
│   ├── Pages/                       # Razor Pages
│   ├── Services/                    # Serviços do frontend
│   └── wwwroot/                     # Arquivos estáticos
│
└── CarTechAssist.Desktop.WinForms/  # Aplicação Desktop
```

## 📦 Pré-requisitos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/sql-server) (2019 ou superior)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) ou [VS Code](https://code.visualstudio.com/) (opcional)
- Conta OpenRouter (para funcionalidades de IA) - [Criar conta em openrouter.ai](https://openrouter.ai) (opcional)
- Conta Google Cloud (para Dialogflow - opcional, usado como fallback)

## ⚙️ Configuração

### 1. Clone o repositório

```bash
git clone <url-do-repositorio>
cd CarTechAssist
```

### 2. Configure o banco de dados

Crie um banco de dados SQL Server:

```sql
CREATE DATABASE CarTechAssist;
```

### 3. Configure as credenciais

#### Opção A: User Secrets (Recomendado para desenvolvimento)

```bash
cd CarTechAssist.Api

# Inicializar User Secrets
dotnet user-secrets init

# Configurar Connection String
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=localhost;Initial Catalog=CarTechAssist;Persist Security Info=True;User ID=sa;Password=SUA_SENHA;Encrypt=False;TrustServerCertificate=True"

# Configurar JWT Secret Key (mínimo 32 caracteres)
dotnet user-secrets set "Jwt:SecretKey" "SUA_CHAVE_SECRETA_MINIMO_32_CARACTERES_AQUI"

# Configurar JWT Issuer (opcional)
dotnet user-secrets set "Jwt:Issuer" "CarTechAssist"

# Configurar JWT Audience (opcional)
dotnet user-secrets set "Jwt:Audience" "CarTechAssist"
```

#### Opção B: Variáveis de Ambiente

**Windows (PowerShell):**
```powershell
$env:ConnectionStrings__DefaultConnection = "Data Source=localhost;Initial Catalog=CarTechAssist;..."
$env:JWT__SecretKey = "SUA_CHAVE_SECRETA"
```

**Linux/Mac:**
```bash
export ConnectionStrings__DefaultConnection="Data Source=localhost;..."
export JWT__SecretKey="SUA_CHAVE_SECRETA"
```

### 4. Configurar OpenRouter (Opcional)

Para usar funcionalidades de IA, configure o OpenRouter no `appsettings.json`:

```json
{
  "OpenRouter": {
    "Enabled": "true",
    "ApiKey": "sk-or-v1-SUA_API_KEY_AQUI",
    "Model": "openai/gpt-4o-mini",
    "MaxTokens": "1000",
    "Temperature": "0.7",
    "HttpReferer": "https://cartechassist.local"
  }
}
```

**Nota:** O OpenRouter é um gateway unificado que permite usar múltiplos modelos de IA (OpenAI, Anthropic, etc.) através de uma única API.

### 5. Executar migrações (se houver)

```bash
# Se usar Entity Framework Migrations
dotnet ef database update --project CarTechAssist.Infrastructure
```

> **Nota**: Para mais detalhes sobre configuração, consulte o arquivo `CarTechAssist.Api/README_CONFIGURACAO.md`

## 🚀 Executando o Projeto

### Executar a API

```bash
cd CarTechAssist.Api
dotnet run
```

A API estará disponível em:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger: `https://localhost:5001/swagger`

### Executar a Aplicação Web

```bash
cd CarTechAssist.Web
dotnet run
```

A aplicação web estará disponível em:
- HTTP: `http://localhost:5095`
- HTTPS: `https://localhost:7045`

### Executar tudo via Visual Studio

1. Abra o arquivo `CarTechAssist.sln`
2. Configure múltiplos projetos de inicialização:
   - Clique com botão direito na Solution
   - Properties → Startup Project → Multiple startup projects
   - Selecione `CarTechAssist.Api` e `CarTechAssist.Web`
3. Pressione F5

## 🔌 API

### Endpoints Principais

#### Autenticação
- `POST /api/Auth/login` - Login de usuário
- `POST /api/Auth/refresh` - Renovar token
- `POST /api/Auth/solicitar-recuperacao` - Solicitar recuperação de senha
- `POST /api/Auth/redefinir-senha` - Redefinir senha

#### Chamados
- `GET /api/Chamados` - Listar chamados (com paginação)
- `GET /api/Chamados/{id}` - Obter detalhes de um chamado
- `POST /api/Chamados` - Criar novo chamado
- `PUT /api/Chamados/{id}` - Atualizar chamado
- `DELETE /api/Chamados/{id}` - Deletar chamado
- `POST /api/Chamados/{id}/atribuir` - Atribuir responsável
- `POST /api/Chamados/{id}/alterar-status` - Alterar status
- `POST /api/Chamados/{id}/interacao` - Adicionar interação
- `GET /api/Chamados/estatisticas` - Obter estatísticas

#### Usuários
- `GET /api/Usuarios` - Listar usuários
- `GET /api/Usuarios/{id}` - Obter usuário
- `POST /api/Usuarios` - Criar usuário
- `PUT /api/Usuarios/{id}` - Atualizar usuário
- `DELETE /api/Usuarios/{id}` - Deletar usuário


#### Categorias
- `GET /api/Categorias` - Listar categorias

### Autenticação

A API utiliza **JWT Bearer Authentication**. Para usar os endpoints protegidos:

1. Faça login em `POST /api/Auth/login`
2. Copie o token da resposta
3. Inclua no header: `Authorization: Bearer {token}`
4. Para endpoints multi-tenant, inclua também: `X-Tenant-Id: {tenantId}`

### Swagger

A documentação interativa da API está disponível em `/swagger` quando executando em modo de desenvolvimento.

## 📚 Documentação

- [Configuração de Segurança](CarTechAssist.Api/README_CONFIGURACAO.md) - Guia completo de configuração
- [Esquema de Camadas](EsquemaDeCamadas.md) - Estrutura detalhada do projeto

## 🔒 Segurança

- ✅ Autenticação JWT com refresh tokens
- ✅ Rate limiting configurável
- ✅ Sanitização de inputs HTML
- ✅ Validação de dados com FluentValidation
- ✅ CORS configurável
- ✅ Headers de segurança
- ✅ Isolamento multi-tenant

## 🤝 Contribuindo

1. Faça um fork do projeto
2. Crie uma branch para sua feature (`git checkout -b feature/AmazingFeature`)
3. Commit suas mudanças (`git commit -m 'Add some AmazingFeature'`)
4. Push para a branch (`git push origin feature/AmazingFeature`)
5. Abra um Pull Request

## 📄 Licença

Este projeto está sob a licença especificada no arquivo `LICENSE.txt`.

## 👥 Autores

- **Equipe CarTechAssist** - Desenvolvimento do projeto

## 🙏 Agradecimentos

- Comunidade .NET
- OpenRouter (Gateway unificado de IA)
- Google Cloud (Dialogflow)

---

**Desenvolvido com ❤️ usando .NET 8.0**

