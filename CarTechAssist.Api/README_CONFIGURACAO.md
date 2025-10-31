# 🔐 Configuração de Segurança - CarTechAssist

## ⚠️ IMPORTANTE - Credenciais Sensíveis

Este projeto utiliza **User Secrets** ou **Variáveis de Ambiente** para proteger credenciais sensíveis. **NUNCA** commite senhas ou chaves secretas no código fonte!

---

## 🚀 Configuração Rápida (Desenvolvimento Local)

### 1. Configurar User Secrets (Recomendado)

Execute os seguintes comandos no terminal, dentro da pasta `CarTechAssist.Api`:

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

### 2. Ou usar Variáveis de Ambiente

No Windows (PowerShell):
```powershell
$env:ConnectionStrings__DefaultConnection = "Data Source=localhost;Initial Catalog=CarTechAssist;..."
$env:JWT__SecretKey = "SUA_CHAVE_SECRETA"
```

No Linux/Mac:
```bash
export ConnectionStrings__DefaultConnection="Data Source=localhost;..."
export JWT__SecretKey="SUA_CHAVE_SECRETA"
```

---

## 📋 Template de Connection String

Use este template como referência:

```
Data Source=localhost;Initial Catalog=CarTechAssist;Persist Security Info=True;User ID=sa;Password=SUA_SENHA_AQUI;Encrypt=False;TrustServerCertificate=True
```

**Atenção**: Em produção, sempre use `Encrypt=True` e `TrustServerCertificate=False`.

---

## 🔑 Requisitos para JWT SecretKey

- **Mínimo**: 32 caracteres
- **Recomendado**: 64+ caracteres
- **Deve ser**: Aleatório e seguro
- **Geração**: Use um gerador seguro de strings aleatórias

Exemplo de geração (PowerShell):
```powershell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

---

## 🛡️ Arquivos Protegidos

Os seguintes arquivos foram configurados para **NÃO** serem versionados:

- `appsettings.Development.json` (adicionado ao .gitignore)
- `appsettings.Production.json` (se existir)

**Template de exemplo**: `appsettings.Development.json.example` (pode ser versionado)

---

## ✅ Verificação

Após configurar, verifique se está funcionando:

```bash
# Rodar a aplicação
dotnet run --project CarTechAssist.Api

# Se funcionar sem erros, a configuração está correta!
```

---

## 🚨 Em Caso de Erro

Se receber erro de "Connection string não configurada" ou "JWT SecretKey não configurada":

1. Verifique se executou os comandos `dotnet user-secrets set`
2. Verifique se está na pasta correta (`CarTechAssist.Api`)
3. Tente usar variáveis de ambiente como alternativa

---

## 🌐 Produção

Em produção (Azure, AWS, etc.), use:
- **Azure Key Vault** (Azure)
- **AWS Secrets Manager** (AWS)
- **Variáveis de Ambiente** do servidor/container
- **App Settings** do serviço de hospedagem

**NUNCA** use arquivos de configuração com credenciais em produção!

