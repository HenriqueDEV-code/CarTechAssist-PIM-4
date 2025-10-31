# 🔐 Configuração de Segurança - CarTechAssist

## ⚠️ IMPORTANTE: Credenciais e Secrets

**NUNCA COMMITE** arquivos com credenciais ou secrets no repositório Git!

### Configuração para Desenvolvimento Local

1. **Criar arquivo `appsettings.Development.json`** baseado no template:
   ```bash
   cp appsettings.Development.json.example appsettings.Development.json
   ```

2. **Preencher as credenciais** no arquivo criado:
   - `ConnectionStrings:DefaultConnection` - String de conexão do SQL Server
   - `Jwt:SecretKey` - Chave secreta para JWT (mínimo 32 caracteres)

3. **Usar User Secrets (Recomendado)** para desenvolvimento:
   ```bash
   cd CarTechAssist.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "SUA_CONNECTION_STRING"
   dotnet user-secrets set "Jwt:SecretKey" "SUA_CHAVE_SECRETA_JWT"
   ```

### Arquivos que DEVEM estar no .gitignore

- `**/appsettings.Development.json`
- `**/appsettings.Local.json`
- `**/appsettings.*.json` (exceto `appsettings.json` e templates `.example`)

### Para Produção

- Use **Azure Key Vault** ou variáveis de ambiente
- Nunca armazene secrets em código-fonte
- Use Managed Identity quando possível

---

## Configurações de Segurança Implementadas

✅ Criptografia de conexão SQL Server habilitada  
✅ JWT com validação completa (Issuer, Audience, Lifetime)  
✅ Validação de tenant em todas as operações críticas  
✅ Autorização baseada em roles  
✅ CORS restritivo por ambiente  
✅ Problemas Details (RFC 7807) para erros padronizados  

