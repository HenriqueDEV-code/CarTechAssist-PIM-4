# Diagnóstico do Problema do ChatBot

## Problema Identificado

Usuário logado como Cliente (TipoUsuarioId = 1) está recebendo erro 403 ao tentar usar o ChatBot.

## Causa Raiz

**INCONSISTÊNCIA ENTRE SESSÃO E JWT TOKEN**

O sistema tem duas verificações de permissão:
1. **Frontend (ChatBot.cshtml.cs)**: Verifica `TipoUsuarioId` na sessão → ✅ PASSA (por isso a página carrega)
2. **API (AuthorizeRolesAttribute)**: Verifica role no JWT token → ❌ FALHA (por isso retorna 403)

### Por que isso acontece?

1. **Token JWT antigo**: Se o usuário fez login antes de configurar JWT na API, o token antigo pode não ter autenticação válida
2. **Token expirado**: O JWT pode ter expirado
3. **Token sem role**: O token pode não estar incluindo a claim de role corretamente

## Solução Imediata

**FAÇA LOGOUT E LOGIN NOVAMENTE**

Isso irá:
- Gerar um novo JWT token com autenticação configurada
- Incluir corretamente a role `"1"` no token
- Sincronizar sessão e token

## Como Verificar se está Funcionando

### 1. Verificar logs da API

Procure por essas mensagens nos logs:
```
[AutorizeRolesAttribute] Acesso negado. Role do usuário: X, Roles permitidas: 1
```

### 2. Verificar JWT token (no navegador)

1. Abra DevTools (F12)
2. Vá em Application/Storage → Session Storage ou Cookies
3. Encontre o `Token` na sessão
4. Cole em https://jwt.io para decodificar
5. Verifique se há uma claim `"role": "1"`

### 3. Testar novamente

Após logout/login, tente enviar uma mensagem no ChatBot novamente.

## Melhorias Implementadas

### 1. Logs Detalhados
- ✅ Logs no frontend mostrando TipoUsuarioId da sessão
- ✅ Logs na API mostrando role do JWT e roles permitidas
- ✅ Detecção de inconsistência entre sessão e JWT

### 2. Mensagens de Erro Melhoradas
- ✅ Mensagem específica quando há inconsistência
- ✅ Instrução para fazer logout/login quando necessário

### 3. Validações Robustas
- ✅ Verificação dupla de permissão (sessão + JWT)
- ✅ Tratamento de erros específicos por tipo

## Próximos Passos

Se o problema persistir após logout/login:

1. Verifique se o usuário no banco realmente tem `TipoUsuarioId = 1`
2. Verifique se a configuração JWT na API está correta
3. Verifique os logs completos da API para identificar exatamente onde está falhando

## Arquivos Modificados

- `CarTechAssist.Web/Pages/ChatBot.cshtml.cs` - Adicionados logs e validações
- `CarTechAssist.Api/Attributes/AuthorizeRolesAttribute.cs` - Melhorados logs e mensagens de erro

