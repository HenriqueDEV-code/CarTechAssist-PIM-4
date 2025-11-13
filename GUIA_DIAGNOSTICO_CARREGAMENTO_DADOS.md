# 🔧 GUIA DE DIAGNÓSTICO - Dados Não Carregando no Frontend

## 📋 Problema Identificado

As seguintes áreas não estão carregando dados:
- **Dashboard**: Mostra todos os valores como "0" e "Nenhum chamado encontrado"
- **Novo Chamado**: 
  - "Nenhuma categoria disponível"
  - "Nenhum usuário disponível" 
  - "Nenhum técnico disponível"

---

## 🔍 CHECKLIST DE DIAGNÓSTICO

### 1. ✅ Verificar se a API está rodando
```bash
# Verificar se a API está respondendo
curl http://localhost:5167/api/categorias
# ou acesse no navegador: http://localhost:5167/swagger
```

**Se não estiver rodando:**
- Execute a API: `dotnet run --project CarTechAssist.Api`

---

### 2. ✅ Verificar Connection String no appsettings.json

**Arquivo:** `CarTechAssist.Api/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=localhost;Initial Catalog=CarTehAssist;..." ### Isso esta com o nome correto
  }
}
```

✅ **CONFIRMADO**: O nome do banco **"CarTehAssist"** está correto conforme sua configuração.

**Se o banco não existir:**
- Crie o banco de dados: `CREATE DATABASE CarTehAssist`
- Execute os scripts de criação de tabelas

---

### 3. ✅ Verificar se o Banco de Dados Existe

**Verificar no SQL Server:**
```sql
-- Conectar ao SQL Server e executar:
SELECT name FROM sys.databases WHERE name = 'CarTechAssist'
-- ou
SELECT name FROM sys.databases WHERE name = 'CarTehAssist'
```

**Se o banco não existir:**
- Crie o banco de dados com o nome correto: `CarTechAssist`
- Execute os scripts de criação de tabelas

**Se o banco existir com nome errado:**
- Opção 1: Renomear o banco para `CarTechAssist`
- Opção 2: Corrigir a connection string para usar o nome atual

---

### 4. ✅ Verificar se há Dados no Banco

**Verificar categorias:**
```sql
SELECT * FROM ref.CategoriaChamado WHERE TenantId = 1 AND Ativo = 1 AND Excluido = 0
```

**Verificar usuários:**
```sql
SELECT * FROM core.Usuario WHERE TenantId = 1 AND Excluido = 0
```

**Verificar chamados:**
```sql
SELECT * FROM core.vw_chamados WHERE TenantId = 1
```

**Se não houver dados:**
- Insira dados de teste no banco
- Ou use o endpoint de setup para criar dados iniciais

---

### 5. ✅ Verificar Logs da API

**Verificar logs no console da API ao acessar:**
- Dashboard
- Novo Chamado

**Procurar por:**
- Erros de conexão com banco
- Erros de autenticação (401, 403)
- Erros de TenantId não encontrado
- Erros de SQL

**Exemplos de logs esperados:**
```
🔍 LISTAR CATEGORIAS - TenantId: 1
✅ LISTAR CATEGORIAS - Sucesso. Total: 5
```

**Se aparecer erro:**
- Anote a mensagem de erro
- Verifique a causa raiz

---

### 6. ✅ Verificar Autenticação (Sessão)

**Verificar se o usuário está logado:**
1. Abra o DevTools do navegador (F12)
2. Vá para a aba "Application" > "Cookies"
3. Verifique se existe o cookie de sessão `ASP.NET_SessionId`

**Verificar valores na sessão:**
- No código, adicione logs temporários ou use breakpoints
- Verifique se `Token`, `TenantId`, `UsuarioId` estão na sessão

**Se a sessão estiver vazia:**
- Faça login novamente
- Verifique se o login está salvando corretamente na sessão

---

### 7. ✅ Verificar Headers nas Requisições HTTP

**No DevTools do navegador:**
1. Abra a aba "Network"
2. Recarregue a página
3. Clique em uma requisição para `/api/categorias` ou `/api/usuarios`
4. Verifique os **Headers** da requisição:

**Headers esperados:**
```
Authorization: Bearer <token>
X-Tenant-Id: 1
X-Usuario-Id: 2
```

**Se os headers estiverem faltando:**
- Verifique se a sessão tem os valores
- Verifique o `ApiClientService.SetHeaders()`

---

### 8. ✅ Testar Endpoints Diretamente

**Testar no Swagger ou Postman:**

**1. Testar Categorias:**
```
GET http://localhost:5167/api/categorias
Headers:
  Authorization: Bearer <seu_token>
  X-Tenant-Id: 1
```

**2. Testar Usuários:**
```
GET http://localhost:5167/api/usuarios?page=1&pageSize=10
Headers:
  Authorization: Bearer <seu_token>
  X-Tenant-Id: 1
```

**3. Testar Estatísticas:**
```
GET http://localhost:5167/api/chamados/estatisticas
Headers:
  Authorization: Bearer <seu_token>
  X-Tenant-Id: 1
```

**Se os endpoints retornarem erro:**
- Anote o código de status (401, 403, 500)
- Anote a mensagem de erro
- Verifique os logs da API

---

## 🛠️ CORREÇÕES MAIS COMUNS

### Correção 1: Connection String com Nome Errado do Banco

**Problema:** `CarTehAssist` em vez de `CarTechAssist`

**Solução:**
1. Abra `CarTechAssist.Api/appsettings.json`
2. Localize a linha com `Initial Catalog=CarTehAssist`
3. Altere para `Initial Catalog=CarTechAssist`
4. Salve e reinicie a API

---

### Correção 2: Banco de Dados Não Existe

**Problema:** O banco de dados não foi criado

**Solução:**
1. Conecte ao SQL Server
2. Execute: `CREATE DATABASE CarTechAssist`
3. Execute os scripts de criação de tabelas
4. Insira dados iniciais

---

### Correção 3: Falta de Dados no Banco

**Problema:** O banco existe mas está vazio

**Solução:**
1. Insira categorias de teste:
```sql
INSERT INTO ref.CategoriaChamado (TenantId, Nome, Ativo, Excluido, DataCriacao)
VALUES (1, 'Suporte Técnico', 1, 0, GETDATE()),
       (1, 'Dúvidas', 1, 0, GETDATE()),
       (1, 'Sugestões', 1, 0, GETDATE())
```

2. Verifique se há usuários no banco
3. Se não houver, crie um usuário admin via endpoint `/api/Setup/criar-admin`

---

### Correção 4: Problema de Autenticação

**Problema:** Token expirado ou inválido

**Solução:**
1. Faça logout
2. Faça login novamente
3. Verifique se o token está sendo salvo na sessão

---

### Correção 5: TenantId Incorreto

**Problema:** O TenantId na sessão não corresponde ao banco

**Solução:**
1. Verifique qual TenantId está na sessão
2. Verifique se há dados para esse TenantId no banco
3. Se necessário, ajuste o TenantId na sessão ou insira dados para o TenantId correto

---

## 📝 PASSOS PARA RESOLVER (Ordem Recomendada)

### Passo 1: Corrigir Connection String
```json
// CarTechAssist.Api/appsettings.json
"DefaultConnection": "Data Source=localhost;Initial Catalog=CarTechAssist;..."
```

### Passo 2: Verificar/Criar Banco de Dados
- Conecte ao SQL Server
- Verifique se o banco `CarTechAssist` existe
- Se não existir, crie-o

### Passo 3: Verificar Dados no Banco
- Execute queries SQL para verificar se há categorias, usuários e chamados
- Se não houver, insira dados de teste

### Passo 4: Reiniciar API
- Pare a API (Ctrl+C)
- Execute novamente: `dotnet run --project CarTechAssist.Api`

### Passo 5: Testar Endpoints
- Acesse o Swagger: `http://localhost:5167/swagger`
- Teste os endpoints manualmente
- Verifique se retornam dados

### Passo 6: Limpar Sessão e Fazer Login Novamente
- No navegador, limpe os cookies
- Faça login novamente
- Verifique se os dados aparecem

---

## 🎯 VERIFICAÇÃO FINAL

Após aplicar as correções, verifique:

1. ✅ API está rodando sem erros
2. ✅ Connection string está correta
3. ✅ Banco de dados existe e tem dados
4. ✅ Endpoints retornam dados no Swagger
5. ✅ Usuário está autenticado (sessão tem Token, TenantId, UsuarioId)
6. ✅ Headers estão sendo enviados nas requisições
7. ✅ Dashboard mostra dados
8. ✅ Formulário de Novo Chamado mostra categorias e usuários

---

## 📞 PRÓXIMOS PASSOS SE AINDA NÃO FUNCIONAR

Se após seguir todos os passos o problema persistir:

1. **Verifique os logs completos da API** - Procure por erros específicos
2. **Verifique os logs do navegador** - Console e Network tabs
3. **Teste cada endpoint individualmente** - Use Postman ou Swagger
4. **Verifique a configuração do CORS** - Pode estar bloqueando requisições
5. **Verifique se há erros de serialização JSON** - Pode haver incompatibilidade de nomes de propriedades

---

**Última atualização:** 2025-01-XX

