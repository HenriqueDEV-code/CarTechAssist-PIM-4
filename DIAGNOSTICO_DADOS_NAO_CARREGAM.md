# 🔍 DIAGNÓSTICO - Por que os dados não estão carregando?

## ✅ Nome do Banco Confirmado
O nome do banco **"CarTehAssist"** está correto conforme sua configuração.

---

## 🔍 CAUSAS POSSÍVEIS (em ordem de probabilidade)

### 1. ⚠️ **BANCO DE DADOS VAZIO** (Mais Provável)

**Verificar se há dados no banco:**

```sql
-- Verificar categorias
SELECT COUNT(*) FROM ref.CategoriaChamado WHERE TenantId = 1 AND Ativo = 1 AND Excluido = 0

-- Verificar usuários
SELECT COUNT(*) FROM core.Usuario WHERE TenantId = 1 AND Excluido = 0

-- Verificar chamados
SELECT COUNT(*) FROM core.vw_chamados WHERE TenantId = 1
```

**Se retornar 0:**
- O banco está vazio
- **Solução:** Inserir dados de teste ou usar o endpoint de setup

**Como inserir dados de teste:**
```sql
-- Inserir categorias
INSERT INTO ref.CategoriaChamado (TenantId, Nome, Ativo, Excluido, DataCriacao)
VALUES 
    (1, 'Suporte Técnico', 1, 0, GETDATE()),
    (1, 'Dúvidas', 1, 0, GETDATE()),
    (1, 'Sugestões', 1, 0, GETDATE()),
    (1, 'Problemas', 1, 0, GETDATE())
```

---

### 2. ⚠️ **PROBLEMA DE AUTENTICAÇÃO**

**Sintomas:**
- Endpoints retornam 401 (Unauthorized) ou 403 (Forbidden)
- Token expirado ou inválido

**Como verificar:**
1. Abra o DevTools do navegador (F12)
2. Vá para a aba **Network**
3. Recarregue a página
4. Clique em uma requisição para `/api/categorias`
5. Verifique o **Status Code**

**Se for 401 ou 403:**
- **Solução:** Faça logout e login novamente
- Verifique se o token está sendo salvo na sessão

---

### 3. ⚠️ **TENANTID INCORRETO**

**Problema:** Os dados podem estar em outro TenantId

**Verificar:**
```sql
-- Ver todos os TenantIds que têm dados
SELECT DISTINCT TenantId FROM ref.CategoriaChamado
SELECT DISTINCT TenantId FROM core.Usuario
SELECT DISTINCT TenantId FROM core.vw_chamados
```

**Verificar qual TenantId está na sessão:**
- No código, adicione um log temporário
- Ou verifique no DevTools > Application > Session Storage

**Se o TenantId da sessão for diferente:**
- Ajuste o TenantId na sessão
- Ou insira dados para o TenantId correto

---

### 4. ⚠️ **CONEXÃO COM BANCO FALHANDO**

**Sintomas:**
- API retorna erro 500
- Logs mostram erro de conexão

**Como verificar:**
1. Veja os logs da API no console
2. Procure por erros como:
   - "Cannot open database"
   - "Login failed"
   - "Timeout expired"

**Soluções:**
- Verifique se o SQL Server está rodando
- Verifique se a senha está correta
- Verifique se o usuário `sa` tem permissões

---

### 5. ⚠️ **ENDPOINTS RETORNANDO VAZIO**

**Problema:** Endpoints funcionam mas retornam array vazio

**Como verificar:**
1. Acesse o Swagger: `http://localhost:5167/swagger`
2. Teste manualmente:
   - `GET /api/categorias`
   - `GET /api/usuarios?page=1&pageSize=10`
   - `GET /api/chamados/estatisticas`

**Se retornar `[]` ou `null`:**
- Verifique se há dados no banco para o TenantId correto
- Verifique os logs da API para ver o que está sendo retornado

---

### 6. ⚠️ **PROBLEMA COM HEADERS**

**Problema:** Headers não estão sendo enviados corretamente

**Como verificar:**
1. DevTools > Network
2. Clique em uma requisição
3. Vá para a aba **Headers**
4. Verifique se tem:
   - `Authorization: Bearer <token>`
   - `X-Tenant-Id: 1`
   - `X-Usuario-Id: <id>`

**Se faltar algum header:**
- Verifique se a sessão tem os valores
- Verifique o `ApiClientService.SetHeaders()`

---

## 🛠️ PASSOS PARA RESOLVER (Ordem Recomendada)

### Passo 1: Verificar se há dados no banco
```sql
SELECT COUNT(*) FROM ref.CategoriaChamado WHERE TenantId = 1 AND Ativo = 1
SELECT COUNT(*) FROM core.Usuario WHERE TenantId = 1 AND Excluido = 0
```

**Se retornar 0:**
- Insira dados de teste (veja exemplos acima)
- Ou use o endpoint `/api/Setup/criar-admin` para criar dados iniciais

---

### Passo 2: Testar endpoints no Swagger
1. Acesse: `http://localhost:5167/swagger`
2. Clique em **Authorize** e cole o token JWT
3. Teste:
   - `GET /api/categorias`
   - `GET /api/usuarios`
   - `GET /api/chamados/estatisticas`

**Se funcionar no Swagger mas não no frontend:**
- Problema está no frontend (headers, sessão, etc.)

**Se não funcionar no Swagger:**
- Problema está na API ou banco de dados

---

### Passo 3: Verificar logs da API
Ao acessar o Dashboard ou Novo Chamado, veja os logs da API:

**Logs esperados:**
```
🔍 LISTAR CATEGORIAS - TenantId: 1
✅ LISTAR CATEGORIAS - Sucesso. Total: 5
```

**Se aparecer erro:**
- Anote a mensagem de erro
- Verifique a causa raiz

---

### Passo 4: Verificar sessão no navegador
1. DevTools > Application > Cookies
2. Verifique se existe `ASP.NET_SessionId`
3. Verifique se a sessão tem:
   - `Token`
   - `TenantId`
   - `UsuarioId`

**Se a sessão estiver vazia:**
- Faça login novamente
- Verifique se o login está salvando na sessão

---

### Passo 5: Verificar requisições HTTP
1. DevTools > Network
2. Recarregue a página
3. Procure por requisições para `/api/categorias`, `/api/usuarios`
4. Verifique:
   - **Status Code** (deve ser 200)
   - **Response** (deve ter dados)
   - **Request Headers** (deve ter Authorization e X-Tenant-Id)

---

## 🎯 CHECKLIST RÁPIDO

Execute este checklist na ordem:

- [ ] 1. Banco de dados tem dados? (Execute queries SQL)
- [ ] 2. API está rodando? (Acesse http://localhost:5167/swagger)
- [ ] 3. Endpoints funcionam no Swagger? (Teste manualmente)
- [ ] 4. Usuário está autenticado? (Sessão tem Token)
- [ ] 5. Headers estão sendo enviados? (DevTools > Network)
- [ ] 6. TenantId está correto? (Verifique na sessão e no banco)
- [ ] 7. Logs da API mostram sucesso? (Veja o console da API)

---

## 📞 SE NADA FUNCIONAR

Se após verificar tudo ainda não funcionar:

1. **Compartilhe os logs da API** - Copie os logs quando acessar o Dashboard
2. **Compartilhe o Response das requisições** - DevTools > Network > Response
3. **Compartilhe o Status Code** - Veja se é 200, 401, 403, 500, etc.
4. **Execute as queries SQL** - Verifique se há dados no banco

---

**Última atualização:** 2025-01-XX

