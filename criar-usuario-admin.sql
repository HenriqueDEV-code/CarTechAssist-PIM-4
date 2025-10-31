-- Script para criar usuário admin com senha
-- Execute este script no SQL Server Management Studio ou Azure Data Studio

USE CarTehAssist;
GO

-- Verificar se o tenant DEMO existe
DECLARE @TenantId INT = (SELECT TOP 1 TenantId FROM ref.Tenant WHERE Codigo='DEMO');
IF @TenantId IS NULL
BEGIN
    PRINT 'Criando tenant DEMO...';
    INSERT INTO ref.Tenant(Nome, Codigo) VALUES (N'Tenant de Demonstração', N'DEMO');
    SET @TenantId = SCOPE_IDENTITY();
END
GO

DECLARE @TenantId INT = (SELECT TOP 1 TenantId FROM ref.Tenant WHERE Codigo='DEMO');

-- Verificar se usuário admin já existe
IF EXISTS (SELECT 1 FROM core.Usuario WHERE TenantId=@TenantId AND Login='admin')
BEGIN
    PRINT 'Usuário admin já existe. Atualizando senha...';
    
    -- Remover usuário existente sem senha
    DELETE FROM core.Usuario WHERE TenantId=@TenantId AND Login='admin' AND (HashSenha IS NULL OR SaltSenha IS NULL);
END
GO

DECLARE @TenantId INT = (SELECT TOP 1 TenantId FROM ref.Tenant WHERE Codigo='DEMO');

-- Criar ou atualizar usuário admin
-- IMPORTANTE: A senha padrão aqui é "Admin@123" - ALTERE ISSO EM PRODUÇÃO!
-- O hash será gerado pela aplicação, mas aqui vamos criar um hash temporário
-- Para facilitar, vamos criar um script que usa a aplicação para gerar o hash

-- Opção 1: Usar API para criar o usuário (RECOMENDADO)
-- Use o endpoint POST /api/usuarios após fazer login com outro método

-- Opção 2: Criar usuário sem senha e depois usar reset de senha
IF NOT EXISTS (SELECT 1 FROM core.Usuario WHERE TenantId=@TenantId AND Login='admin')
BEGIN
    PRINT 'Criando usuário admin...';
    INSERT INTO core.Usuario (TenantId, TipoUsuarioId, Login, NomeCompleto, Email, Ativo, PrecisaTrocarSenha)
    VALUES (@TenantId, 3, 'admin', N'Administrador', 'admin@demo.local', 1, 1);
    PRINT 'Usuário admin criado. Use o endpoint de reset de senha para definir a senha.';
END
ELSE
BEGIN
    PRINT 'Usuário admin já existe.';
    -- Ativar usuário caso esteja inativo
    UPDATE core.Usuario 
    SET Ativo = 1, Excluido = 0 
    WHERE TenantId=@TenantId AND Login='admin';
END
GO

PRINT 'Script concluído!';
PRINT 'NOTA: Você precisa usar a API para definir a senha do usuário admin.';
PRINT 'O campo HashSenha precisa ser gerado pela aplicação usando o mesmo algoritmo.';

