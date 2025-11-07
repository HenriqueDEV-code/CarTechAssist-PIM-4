# 🔍 Diagnóstico Completo da API de Criação de Usuários

## ✅ Correções Aplicadas

### 1. **Campo `PrecisaTrocarSenha` Ausente**
- **Problema:** O objeto `Usuario` não definia `PrecisaTrocarSenha`, mas o SQL esperava esse campo.
- **Correção:** Adicionado `PrecisaTrocarSenha = false` na criação do usuário em `UsuariosService.cs`.

### 2. **Logs Detalhados em Todas as Camadas**
- **Controller:** Logs com emojis para fácil identificação (🔴 CONTROLLER, 🎉 SUCESSO, ⚠️ AVISO, ❌ ERRO)
- **Service:** Logs detalhados de cada etapa (validação, hash, criação)
- **Repository:** Logs de conexão, SQL e execução

### 3. **Verificação de Conexão**
- **Problema:** Conexão pode não estar aberta quando o Dapper tenta executar.
- **Correção:** Verificação e abertura automática da conexão no repositório.

### 4. **Validações Aprimoradas**
- Validação de campos obrigatórios no Controller
- Validação de tipo de usuário (Agente/Admin vs Cliente)
- Validação de senha mínima

### 5. **Tratamento de Erros Melhorado**
- Logs detalhados de exceções
- Mensagens de erro mais específicas
- StackTrace completo nos logs

---

## 📊 Fluxo de Criação de Usuário

### **Registro de Cliente (Público)**
```
1. Frontend (Register.cshtml.cs)
   └─> UsuariosService.CriarPublicoAsync()
       └─> ApiClientService.PostAsyncSemAuth()
           └─> POST /api/usuarios/registro-publico
               └─> UsuariosController.RegistroPublico()
                   └─> UsuariosService.CriarAsync()
                       └─> UsuariosRepository.CriarAsync()
                           └─> SQL INSERT INTO core.Usuario
```

### **Criação de Admin/Agente (Autenticado)**
```
1. Frontend (Usuarios.cshtml.cs)
   └─> UsuariosService.CriarAsync()
       └─> ApiClientService.PostAsync()
           └─> POST /api/usuarios
               └─> UsuariosController.Criar()
                   └─> UsuariosService.CriarAsync()
                       └─> UsuariosRepository.CriarAsync()
                           └─> SQL INSERT INTO core.Usuario
```

---

## 🔍 Como Diagnosticar Problemas

### **1. Verificar Logs da API**

Os logs agora mostram cada etapa do processo com emojis:

- 🔴 **CONTROLLER**: Requisição recebida
- 🟢 **SERVICE**: Processamento no service
- 🔵 **REPOSITORY**: Execução no banco de dados
- ✅ **SUCESSO**: Operação concluída
- ⚠️ **AVISO**: Problema não crítico
- ❌ **ERRO**: Falha na operação

### **2. Verificar Connection String**

Verifique se a connection string está correta em `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=localhost;Initial Catalog=CarTehAssist;..."
  }
}
```

**⚠️ ATENÇÃO:** O nome do banco está como `CarTehAssist` (com "h" minúsculo). Verifique se o banco de dados existe com esse nome exato.

### **3. Verificar Permissões do Banco**

Certifique-se de que o usuário `sa` tem permissões para:
- INSERT na tabela `core.Usuario`
- SELECT na tabela `core.Usuario` (para verificar login único)

### **4. Verificar Estrutura da Tabela**

Execute este SQL para verificar se a tabela existe e tem a estrutura correta:

```sql
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'core' 
  AND TABLE_NAME = 'Usuario'
ORDER BY ORDINAL_POSITION;
```

**Campos obrigatórios esperados:**
- `TenantId` (int)
- `TipoUsuarioId` (tinyint)
- `Login` (nvarchar)
- `NomeCompleto` (nvarchar)
- `Email` (nvarchar, nullable)
- `Telefone` (nvarchar, nullable)
- `HashSenha` (varbinary)
- `SaltSenha` (varbinary)
- `PrecisaTrocarSenha` (bit)
- `Ativo` (bit)
- `DataCriacao` (datetime)
- `Excluido` (bit)

---

## 🧪 Testes Recomendados

### **1. Teste de Conexão**
Execute este comando para verificar se a API consegue conectar ao banco:

```bash
# Verificar health check
curl http://localhost:5000/health
```

### **2. Teste de Registro Público**
```bash
curl -X POST http://localhost:5000/api/usuarios/registro-publico \
  -H "Content-Type: application/json" \
  -d '{
    "login": "teste",
    "nomeCompleto": "Teste Usuario",
    "email": "teste@teste.com",
    "telefone": null,
    "tipoUsuarioId": 1,
    "senha": "123456"
  }'
```

### **3. Verificar Logs**
Após executar o teste, verifique os logs da API. Você deve ver:

```
🔴 CONTROLLER (PUBLICO): Recebida requisição de registro público...
🔴 CONTROLLER (PUBLICO): Validações passadas...
🟢 SERVICE: Iniciando criação de usuário...
🔍 Verificando se login já existe...
✅ Login disponível.
🔐 Gerando hash da senha...
✅ Hash gerado...
📦 Objeto Usuario criado. Chamando repositório...
🔵 REPOSITORY: Iniciando criação de usuário...
✅ Conexão aberta...
📝 SQL preparado. Executando INSERT...
✅ INSERT executado com sucesso! UsuarioId retornado: X
🎉 Usuário criado com sucesso no banco de dados!
```

---

## 🐛 Problemas Comuns e Soluções

### **Problema 1: "Login já está em uso"**
- **Causa:** Login já existe no banco de dados.
- **Solução:** Use outro login ou verifique se o usuário foi realmente criado.

### **Problema 2: "TenantId não encontrado"**
- **Causa:** Header `X-Tenant-Id` não está sendo enviado ou está inválido.
- **Solução:** Verifique se o frontend está enviando o header corretamente.

### **Problema 3: "Conexão não está aberta"**
- **Causa:** Dapper pode não abrir a conexão automaticamente em alguns casos.
- **Solução:** Já corrigido - o código agora verifica e abre a conexão automaticamente.

### **Problema 4: "Erro ao executar SQL"**
- **Causa:** Problema com a estrutura da tabela ou permissões.
- **Solução:** Verifique a estrutura da tabela e as permissões do usuário do banco.

### **Problema 5: "UsuarioId retornado é 0"**
- **Causa:** O INSERT não retornou um ID válido.
- **Solução:** Verifique se a tabela tem uma coluna de identidade configurada corretamente.

---

## 📝 Próximos Passos

1. **Execute a API** e monitore os logs
2. **Tente criar um usuário** (cliente ou admin/agente)
3. **Verifique os logs** para ver onde está falhando (se estiver falhando)
4. **Verifique o banco de dados** diretamente para confirmar se o usuário foi criado

---

## ✅ Checklist de Verificação

- [ ] Connection string está correta
- [ ] Banco de dados existe e está acessível
- [ ] Tabela `core.Usuario` existe e tem a estrutura correta
- [ ] Usuário do banco tem permissões adequadas
- [ ] Logs estão configurados para Debug
- [ ] API está rodando e acessível
- [ ] Frontend está enviando requisições corretamente

---

**Data:** 2025-01-XX  
**Status:** ✅ Diagnóstico completo implementado com logs detalhados

