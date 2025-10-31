# 📚 DOCUMENTAÇÃO COMPLETA - CarTechAssist

## 📋 ÍNDICE

1. [Visão Geral do Projeto](#visão-geral)
2. [Arquitetura do Sistema](#arquitetura)
3. [Estrutura de Projetos](#estrutura-de-projetos)
4. [Funcionalidades Implementadas](#funcionalidades-implementadas)
5. [Funcionalidades Pendentes](#funcionalidades-pendentes)
6. [APIs e Endpoints](#apis-e-endpoints)
7. [Banco de Dados](#banco-de-dados)
8. [Autenticação e Segurança](#autenticação-e-segurança)
9. [Documentação Técnica](#documentação-técnica)

---

## 🎯 VISÃO GERAL

**CarTechAssist** é um sistema de gerenciamento de chamados (tickets) para assistência técnica automotiva, desenvolvido em .NET 8.0 com arquitetura em camadas (Clean Architecture).

### Características Principais

- ✅ Multi-tenant (suporte a múltiplas empresas/tenants)
- ✅ Autenticação JWT com roles
- ✅ Interface Web (Razor Pages)
- ✅ API RESTful completa
- ✅ Integração com IA para categorização e priorização
- ✅ Sistema de recuperação de senha
- ✅ Gerenciamento de SLA
- ✅ Histórico de status e auditoria

---

## 🏗️ ARQUITETURA

### Padrão Arquitetural
**Clean Architecture** com separação em camadas:

```
┌─────────────────────────────────────┐
│   CarTechAssist.Web (UI Layer)     │  ← Apresentação (Razor Pages)
├─────────────────────────────────────┤
│   CarTechAssist.Api (API Layer)    │  ← Controllers REST
├─────────────────────────────────────┤
│   CarTechAssist.Application        │  ← Lógica de Negócio (Services)
├─────────────────────────────────────┤
│   CarTechAssist.Domain              │  ← Entidades e Interfaces
├─────────────────────────────────────┤
│   CarTechAssist.Infrastruture       │  ← Acesso a Dados (Repositories)
├─────────────────────────────────────┤
│   CarTechAssist.Contracts          │  ← DTOs e Contratos
└─────────────────────────────────────┘
```

### Stack Tecnológico

- **Backend:** ASP.NET Core 8.0 (API + Web)
- **ORM:** Dapper
- **Banco de Dados:** SQL Server
- **Autenticação:** JWT (JSON Web Tokens)
- **Validação:** FluentValidation
- **Email:** SMTP (Gmail)
- **Frontend:** Razor Pages + Bootstrap 5

---

## 📦 ESTRUTURA DE PROJETOS

### 1. CarTechAssist.Domain
**Responsabilidade:** Entidades de domínio, enums e interfaces.

#### Entidades (12 entidades)
- ✅ `Chamado` - Entidade principal de tickets
- ✅ `Usuario` - Usuários do sistema
- ✅ `Tenant` - Empresas/Organizações
- ✅ `CategoriaChamado` - Categorias de tickets
- ✅ `ChamadoInteracao` - Mensagens/comentários em tickets
- ✅ `ChamadoAnexo` - Anexos de tickets
- ✅ `ChamadoStatusHistorico` - Histórico de mudanças de status
- ✅ `IAFeedback` - Feedbacks da IA
- ✅ `IARunLog` - Logs de execução da IA
- ✅ `RecuperacaoSenha` - Códigos de recuperação
- ✅ `Auditoria` - Log de auditoria
- ✅ `ApiClient` - Clientes da API

#### Enums (5 enums)
- ✅ `StatusChamado` - Estados do chamado
- ✅ `PrioridadeChamado` - Níveis de prioridade
- ✅ `TipoUsuarios` - Tipos de usuário (Cliente, Técnico, Admin)
- ✅ `CanalAtendimento` - Canais de entrada
- ✅ `IAFeedbackScore` - Score de feedback da IA

#### Interfaces (7 interfaces)
- ✅ `IChamadosRepository`
- ✅ `IUsuariosRepository`
- ✅ `ICategoriasRepository`
- ✅ `IRecuperacaoSenhaRepository`
- ✅ `IAnexosReposity`
- ✅ `IAiProvider`
- ✅ `IAditoriaRepository`

### 2. CarTechAssist.Contracts
**Responsabilidade:** DTOs (Data Transfer Objects) para comunicação entre camadas.

#### Estrutura de DTOs
- **Auth/** - LoginRequest, LoginResponse, RefreshTokenRequest, etc.
- **Usuarios/** - UsuarioDto, CriarUsuarioRequest, etc.
- **Tickets/** - TicketView, CriarChamadoRequest, AtualizarChamadoRequest, etc.
- **Feedback/** - EnviarFeedbackRequest, FeedbackDto
- **Common/** - PagedResult (paginção)

### 3. CarTechAssist.Application
**Responsabilidade:** Lógica de negócio e serviços.

#### Services (7 services)
- ✅ `ChamadosService` - Gerenciamento de chamados
- ✅ `UsuariosService` - Gerenciamento de usuários
- ✅ `AuthService` - Autenticação e JWT
- ✅ `CategoriasService` - Gerenciamento de categorias
- ✅ `EmailService` - Envio de emails
- ✅ `RecuperacaoSenhaService` - Recuperação de senha
- ✅ `IAProvider` (não implementado - interface apenas)

#### Validators (4 validators)
- ✅ `LoginRequestValidator`
- ✅ `CriarUsuarioRequestValidator`
- ✅ `CriarChamadoRequestValidator`
- ✅ `EnviarFeedbackRequestValidator`

### 4. CarTechAssist.Infrastruture
**Responsabilidade:** Implementação de repositórios e acesso a dados.

#### Repositories (5 repositories)
- ✅ `ChamadosRepository` - Acesso a dados de chamados
- ✅ `UsuariosRepository` - Acesso a dados de usuários
- ✅ `CategoriasRepository` - Acesso a dados de categorias
- ✅ `RecuperacaoSenhaRepository` - Acesso a dados de recuperação
- ✅ `AnexosRepository` - Acesso a dados de anexos

### 5. CarTechAssist.Api
**Responsabilidade:** Controllers REST e configuração da API.

#### Controllers (7 controllers)
- ✅ `ChamadosController` - CRUD de chamados
- ✅ `UsuariosController` - CRUD de usuários
- ✅ `AuthController` - Autenticação
- ✅ `CategoriasController` - Listagem de categorias
- ✅ `RecuperacaoSenhaController` - Recuperação de senha
- ✅ `EmailTestController` - Teste de email (desenvolvimento)
- ✅ `SetupController` - Setup inicial (temporário)

#### Middleware (2 middlewares)
- ✅ `GlobalExceptionHandlerMiddleware` - Tratamento global de exceções
- ✅ `TenantMiddleware` - Extração de TenantId do JWT

### 6. CarTechAssist.Web
**Responsabilidade:** Interface web com Razor Pages.

#### Pages Implementadas (10 pages)
- ✅ `Login.cshtml` - Página de login
- ✅ `Register.cshtml` - Registro de clientes
- ✅ `Dashboard.cshtml` - Dashboard principal
- ✅ `ForgotPassword.cshtml` - Solicitar recuperação
- ✅ `ResetPassword.cshtml` - Redefinir senha
- ✅ `Index.cshtml` - Página inicial
- ✅ `Error.cshtml` - Página de erro
- ✅ `Privacy.cshtml` - Página de privacidade
- ⚠️ `Chamados/` - **PENDENTE** (apenas estrutura)
- ⚠️ `Usuarios/` - **PENDENTE** (apenas estrutura)

#### Services (5 services)
- ✅ `ApiClientService` - Cliente HTTP para API
- ✅ `AuthService` - Serviço de autenticação (Web)
- ✅ `ChamadosService` - Serviço de chamados (Web)
- ✅ `UsuariosService` - Serviço de usuários (Web)
- ✅ `CategoriasService` - Serviço de categorias (Web)

### 7. CarTechAssist.Desktop.WinForms
**Status:** ⚠️ **Projeto criado mas não implementado**
- Apenas `Form1.cs` vazio

---

## ✅ FUNCIONALIDADES IMPLEMENTADAS

### 🔐 Autenticação e Autorização

#### Implementado ✅
- [x] Login com JWT
- [x] Geração de Access Token e Refresh Token
- [x] Validação de JWT em endpoints protegidos
- [x] Sistema de roles (Cliente, Técnico, Administrador)
- [x] Middleware de extração de TenantId e UsuarioId
- [x] Endpoint `/api/Auth/me` para informações do usuário
- [x] Endpoint `/api/Auth/refresh` para renovar token

#### Pendente ❌
- [ ] Logout (limpar tokens no servidor)
- [ ] Controle de sessão mais robusto
- [ ] Refresh token com rotação

### 👥 Gerenciamento de Usuários

#### Implementado ✅
- [x] CRUD de usuários (API)
- [x] Criação de usuários por tipo (Cliente, Técnico, Admin)
- [x] Validação de permissões (Admin pode criar técnicos)
- [x] Clientes podem se registrar publicamente
- [x] Email obrigatório para clientes
- [x] Hash de senha com HMACSHA512
- [x] Listagem paginada
- [x] Filtros por tipo e status

#### Pendente ❌
- [ ] Interface Web completa para gerenciar usuários
- [ ] Atualização de perfil
- [ ] Troca de senha
- [ ] Desativação/ativação de usuários (UI)

### 🎫 Sistema de Chamados (Tickets)

#### Implementado ✅
- [x] **CRUD Completo:**
  - Criar chamado
  - Listar chamados (com paginação)
  - Obter chamado por ID
  - Atualizar chamado (PUT)
  - Alterar status (PATCH)
  - Atribuir responsável (PATCH)
  - Adicionar feedback da IA
  
- [x] **Validações de Negócio:**
  - Validação de transições de status
  - Permissões por role (Cliente só vê seus chamados)
  - Validação de integridade referencial
  - Cálculo automático de SLA baseado em prioridade
  
- [x] **Filtros e Busca:**
  - Filtro por status
  - Filtro por responsável
  - Filtro por solicitante
  - Paginação
  
- [x] **SLA (Service Level Agreement):**
  - Cálculo automático ao atribuir responsável
  - Urgente: 4 horas
  - Alta: 8 horas
  - Média: 24 horas
  - Baixa: 72 horas

- [x] **Integração com IA:**
  - Endpoint para adicionar feedback da IA
  - Suporte a sugestões de categoria e prioridade

#### Pendente ❌
- [ ] Interface Web para criar/editar chamados
- [ ] Interface Web para listar chamados
- [ ] Interface Web para visualizar detalhes do chamado
- [ ] Sistema de interações/comentários (UI)
- [ ] Upload de anexos (UI)
- [ ] Dashboard com gráficos e estatísticas

### 📧 Sistema de Email

#### Implementado ✅
- [x] EmailService completo com SMTP
- [x] Envio de emails HTML
- [x] Template profissional para recuperação de senha
- [x] Tratamento de exceções detalhado
- [x] Logs detalhados
- [x] Endpoint de teste (`/api/EmailTest/testar`)

#### Pendente ❌
- [ ] Configuração via appsettings (hoje está hardcoded)
- [ ] Suporte a múltiplos provedores de email
- [ ] Fila de emails (para envio assíncrono)
- [ ] Templates para outros tipos de email

### 🔑 Recuperação de Senha

#### Implementado ✅
- [x] Solicitar código de recuperação
- [x] Geração de código de 6 dígitos
- [x] Validação de código
- [x] Redefinição de senha
- [x] Código válido por 30 minutos
- [x] Limpeza automática de códigos expirados
- [x] Interface Web completa (ForgotPassword + ResetPassword)
- [x] Email com código (aguardando reset Gmail)

### 📁 Categorias

#### Implementado ✅
- [x] Listagem de categorias ativas
- [x] Suporte a hierarquia (categorias pai/filho)
- [x] Validação de categoria ao criar/atualizar chamado

#### Pendente ❌
- [ ] CRUD completo de categorias (API)
- [ ] Interface Web para gerenciar categorias
- [ ] Validação de hierarquia

### 📊 Dashboard

#### Implementado ✅
- [x] Dashboard básico (`/Dashboard`)
- [x] Exibição de informações do usuário
- [x] Estatísticas básicas (total de chamados, por status)
- [x] Lista de chamados recentes

#### Pendente ❌
- [ ] Gráficos e visualizações
- [ ] Filtros por período
- [ ] Métricas de SLA
- [ ] Gráfico de chamados por status
- [ ] Gráfico de chamados por categoria
- [ ] Performance de técnicos

### 🛡️ Segurança

#### Implementado ✅
- [x] Autenticação JWT
- [x] Hash de senha com salt (HMACSHA512)
- [x] Validação de permissões por role
- [x] Middleware de tratamento global de exceções
- [x] CORS configurado
- [x] Validação de entrada (FluentValidation)
- [x] Multi-tenant com isolamento de dados

#### Pendente ❌
- [ ] Rate limiting
- [ ] Proteção CSRF
- [ ] Sanitização de entrada
- [ ] Logs de auditoria mais detalhados
- [ ] Backup automático

### 🗄️ Banco de Dados

#### Tabelas Implementadas
- ✅ `core.Usuario`
- ✅ `core.Chamado`
- ✅ `core.CategoriaChamado`
- ✅ `core.ChamadoInteracao`
- ✅ `core.ChamadoAnexo`
- ✅ `core.ChamadoStatusHistorico`
- ✅ `core.IAFeedback`
- ✅ `core.IARunLog`
- ✅ `core.RecuperacaoSenha`
- ✅ `log.Auditoria`
- ✅ `ref.Tenant`
- ✅ `ref.ApiClient`

---

## ❌ FUNCIONALIDADES PENDENTES

### 🔴 Críticas (Alta Prioridade)

#### 1. Interface Web de Chamados
**Status:** ⚠️ **FALTA COMPLETAR**

**O que falta:**
- [ ] Listagem de chamados com filtros
- [ ] Formulário de criação de chamado
- [ ] Página de detalhes do chamado
- [ ] Edição de chamado
- [ ] Alteração de status (UI)
- [ ] Sistema de interações/comentários
- [ ] Upload de anexos

**Onde implementar:**
- `CarTechAssist.Web/Pages/Chamados/Listar.cshtml`
- `CarTechAssist.Web/Pages/Chamados/Criar.cshtml`
- `CarTechAssist.Web/Pages/Chamados/Detalhes.cshtml`
- `CarTechAssist.Web/Pages/Chamados/Editar.cshtml`

#### 2. Interface Web de Usuários
**Status:** ⚠️ **FALTA COMPLETAR**

**O que falta:**
- [ ] Listagem de usuários (já existe API)
- [ ] Formulário de criação de usuário
- [ ] Edição de usuário
- [ ] Ativação/desativação de usuários
- [ ] Reset de senha por admin

**Onde implementar:**
- `CarTechAssist.Web/Pages/Usuarios/Listar.cshtml`
- `CarTechAssist.Web/Pages/Usuarios/Criar.cshtml`
- `CarTechAssist.Web/Pages/Usuarios/Editar.cshtml`

#### 3. Sistema de Anexos
**Status:** ⚠️ **PARCIAL**

**O que existe:**
- ✅ Entity `ChamadoAnexo`
- ✅ Repository `AnexosRepository`
- ✅ Interface `IAnexosReposity`

**O que falta:**
- [ ] Endpoint para upload de anexos
- [ ] Endpoint para download de anexos
- [ ] Armazenamento de arquivos (local ou blob storage)
- [ ] Validação de tipos de arquivo
- [ ] Validação de tamanho máximo
- [ ] Interface Web para upload/download

#### 4. Sistema de Interações/Comentários
**Status:** ⚠️ **PARCIAL**

**O que existe:**
- ✅ Entity `ChamadoInteracao`
- ✅ Estrutura no banco

**O que falta:**
- [ ] Endpoints CRUD de interações
- [ ] Service para gerenciar interações
- [ ] Interface Web para adicionar comentários
- [ ] Diferença entre comentários públicos e internos
- [ ] Notificações de novas interações

### 🟡 Importantes (Média Prioridade)

#### 5. Dashboard Avançado
**Status:** ⚠️ **BÁSICO IMPLEMENTADO**

**O que falta:**
- [ ] Gráficos com Chart.js ou similar
- [ ] Métricas de SLA (taxa de cumprimento)
- [ ] Chamados por status (gráfico de pizza)
- [ ] Chamados por categoria (gráfico de barras)
- [ ] Performance de técnicos
- [ ] Tempo médio de resolução
- [ ] Filtros por período (diário, semanal, mensal)

#### 6. Integração com IA
**Status:** ⚠️ **ESTRUTURA EXISTE**

**O que existe:**
- ✅ Entity `IAFeedback`
- ✅ Entity `IARunLog`
- ✅ Interface `IAiProvider`
- ✅ Campos no `Chamado` para dados da IA

**O que falta:**
- [ ] Implementação concreta de `IAiProvider`
- [ ] Integração com OpenAI, Azure OpenAI, ou similar
- [ ] Processamento automático de novos chamados
- [ ] Sugestão de categoria e prioridade
- [ ] Resumo automático do chamado
- [ ] Interface para visualizar sugestões da IA

#### 7. Sistema de Notificações
**Status:** ❌ **NÃO IMPLEMENTADO**

**O que falta:**
- [ ] Tabela de notificações
- [ ] Service de notificações
- [ ] Endpoints de notificações
- [ ] Interface Web de notificações
- [ ] Notificações por email
- [ ] Notificações em tempo real (SignalR)

#### 8. Relatórios
**Status:** ❌ **NÃO IMPLEMENTADO**

**O que falta:**
- [ ] Geração de relatórios (PDF/Excel)
- [ ] Relatório de chamados por período
- [ ] Relatório de performance de técnicos
- [ ] Relatório de SLA
- [ ] Exportação de dados

### 🟢 Melhorias (Baixa Prioridade)

#### 9. Auditoria Completa
**Status:** ⚠️ **PARCIAL**

**O que existe:**
- ✅ Entity `Auditoria`
- ✅ Interface `IAditoriaRepository`

**O que falta:**
- [ ] Implementação do repository
- [ ] Registro automático de mudanças
- [ ] Interface Web para visualizar auditoria
- [ ] Filtros e busca na auditoria

#### 10. Desktop Application
**Status:** ⚠️ **PROJETO CRIADO MAS VAZIO**

**O que falta:**
- [ ] Implementação completa do WinForms
- [ ] Integração com API
- [ ] Interface desktop funcional

#### 11. Testes
**Status:** ❌ **NÃO IMPLEMENTADO**

**O que falta:**
- [ ] Testes unitários
- [ ] Testes de integração
- [ ] Testes de API
- [ ] Cobertura de código

#### 12. Documentação da API
**Status:** ⚠️ **SWAGGER BÁSICO**

**O que existe:**
- ✅ Swagger UI configurado

**O que falta:**
- [ ] Documentação XML dos endpoints
- [ ] Exemplos de request/response
- [ ] Descrição detalhada de cada endpoint
- [ ] Códigos de status HTTP documentados

---

## 🔌 APIs E ENDPOINTS

### Autenticação (`/api/Auth`)
- ✅ `POST /api/Auth/login` - Login
- ✅ `POST /api/Auth/refresh` - Renovar token
- ✅ `GET /api/Auth/me` - Informações do usuário logado

### Usuários (`/api/Usuarios`)
- ✅ `GET /api/Usuarios` - Listar usuários (paginado, filtros)
- ✅ `GET /api/Usuarios/{id}` - Obter usuário por ID
- ✅ `POST /api/Usuarios` - Criar usuário

### Chamados (`/api/Chamados`)
- ✅ `GET /api/Chamados` - Listar chamados (paginado, filtros)
- ✅ `GET /api/Chamados/{id}` - Obter chamado por ID
- ✅ `POST /api/Chamados` - Criar chamado
- ✅ `PUT /api/Chamados/{id}` - Atualizar chamado
- ✅ `PATCH /api/Chamados/{id}/status` - Alterar status
- ✅ `PATCH /api/Chamados/{id}/responsavel` - Atribuir responsável
- ✅ `POST /api/Chamados/{id}/feedback` - Adicionar feedback da IA

### Categorias (`/api/Categorias`)
- ✅ `GET /api/Categorias` - Listar categorias ativas

### Recuperação de Senha (`/api/RecuperacaoSenha`)
- ✅ `POST /api/RecuperacaoSenha/solicitar` - Solicitar código
- ✅ `POST /api/RecuperacaoSenha/validar-codigo` - Validar código
- ✅ `POST /api/RecuperacaoSenha/redefinir` - Redefinir senha

### Setup (`/api/Setup`) - ⚠️ TEMPORÁRIO
- ✅ `POST /api/Setup/criar-admin` - Criar admin (dev only)
- ✅ `POST /api/Setup/definir-senha-admin` - Definir senha admin (dev only)

### Teste (`/api/EmailTest`)
- ✅ `POST /api/EmailTest/testar` - Testar envio de email (dev only)

---

## 🗄️ BANCO DE DADOS

### Schema `core`
- `Usuario` - Usuários do sistema
- `Chamado` - Chamados/tickets
- `CategoriaChamado` - Categorias
- `ChamadoInteracao` - Interações/comentários
- `ChamadoAnexo` - Anexos
- `ChamadoStatusHistorico` - Histórico de status
- `IAFeedback` - Feedbacks da IA
- `IARunLog` - Logs da IA
- `RecuperacaoSenha` - Códigos de recuperação

### Schema `ref`
- `Tenant` - Empresas/Organizações
- `ApiClient` - Clientes da API

### Schema `log`
- `Auditoria` - Log de auditoria

### Scripts SQL Disponíveis
- ✅ `criar-usuario-admin.sql` - Criar primeiro admin
- ✅ `criar-tabela-recuperacao-senha.sql` - Criar tabela de recuperação

---

## 🔐 AUTENTICAÇÃO E SEGURANÇA

### JWT Configuration
- **Secret Key:** Configurado em `appsettings.Development.json`
- **Issuer:** CarTechAssist
- **Audience:** CarTechAssist
- **Expiration:** Access Token (1h), Refresh Token (7 dias)

### Roles (Tipos de Usuário)
1. **Cliente (1)** - Pode criar chamados, ver apenas seus chamados
2. **Técnico (2)** - Pode gerenciar chamados atribuídos a ele
3. **Administrador (3)** - Acesso total ao sistema

### Validações
- ✅ FluentValidation para todos os requests
- ✅ Validação de permissões por role
- ✅ Validação de transições de status
- ✅ Validação de integridade referencial
- ✅ Hash de senha com salt

---

## 📝 DOCUMENTAÇÃO TÉCNICA

### Configuração

#### API (`CarTechAssist.Api`)
- **Porta HTTP:** 5167
- **Porta HTTPS:** 7294
- **Swagger:** `https://localhost:7294/swagger`

#### Web (`CarTechAssist.Web`)
- **Porta HTTP:** 5095
- **Porta HTTPS:** 7045

#### Banco de Dados
- **Connection String:** Configurada em `appsettings.Development.json`
- **Database:** `CarTehAssist`

### Variáveis de Ambiente
- `ConnectionStrings:DefaultConnection` - String de conexão SQL Server
- `Jwt:SecretKey` - Chave secreta JWT (mínimo 32 caracteres)
- `Jwt:Issuer` - Emissor do token
- `Jwt:Audience` - Audiência do token

### Dependências Principais
- `Microsoft.AspNetCore.Authentication.JwtBearer` - JWT
- `Dapper` - Micro-ORM
- `FluentValidation.AspNetCore` - Validação
- `System.Data.SqlClient` - SQL Server

---

## 📊 ESTATÍSTICAS DO PROJETO

### Código Implementado
- **Controllers:** 7
- **Services:** 7
- **Repositories:** 5
- **Entities:** 12
- **DTOs:** ~30+
- **Validators:** 4
- **Razor Pages:** 10 (5 completas, 5 pendentes)
- **Middleware:** 2

### Funcionalidades
- ✅ **Implementadas:** ~70%
- ⚠️ **Parciais:** ~15%
- ❌ **Pendentes:** ~15%

---

## 🚀 PRÓXIMOS PASSOS RECOMENDADOS

### Fase 1: Completar UI (Alta Prioridade)
1. Implementar páginas de Chamados (Listar, Criar, Editar, Detalhes)
2. Implementar páginas de Usuários (Listar, Criar, Editar)
3. Adicionar upload/download de anexos
4. Implementar sistema de interações/comentários

### Fase 2: Funcionalidades Avançadas
1. Dashboard com gráficos
2. Sistema de notificações
3. Relatórios e exportação
4. Integração completa com IA

### Fase 3: Melhorias e Otimizações
1. Testes automatizados
2. Documentação completa da API
3. Otimizações de performance
4. Auditoria completa

---

## 📞 SUPORTE

### Arquivos de Documentação Adicionais
- `INSTRUCOES_FUNCIONALIDADES.md` - Guia de funcionalidades
- `COMO_DEFINIR_SENHA_ADMIN.md` - Setup inicial
- `SOLUCAO_LIMITE_GMAIL.md` - Problema de limite de email
- `RESUMO_FUNCIONALIDADES_EMAIL.md` - Sistema de email

---

**Última Atualização:** 31/10/2025  
**Versão do Projeto:** 1.0.0  
**Status Geral:** ✅ Funcional, algumas funcionalidades pendentes

