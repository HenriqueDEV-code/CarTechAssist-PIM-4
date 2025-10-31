-- Script para criar tabela de recuperação de senha
USE CarTehAssist;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[core].[RecuperacaoSenha]') AND type in (N'U'))
BEGIN
    CREATE TABLE [core].[RecuperacaoSenha] (
        [RecuperacaoSenhaId] BIGINT IDENTITY(1,1) NOT NULL,
        [TenantId] INT NOT NULL,
        [UsuarioId] INT NOT NULL,
        [Codigo] VARCHAR(10) NOT NULL,
        [Email] NVARCHAR(255) NOT NULL,
        [DataExpiracao] DATETIME2 NOT NULL,
        [Usado] BIT NOT NULL DEFAULT 0,
        [DataCriacao] DATETIME2 NOT NULL DEFAULT GETDATE(),
        [DataUso] DATETIME2 NULL,
        
        CONSTRAINT [PK_RecuperacaoSenha] PRIMARY KEY CLUSTERED ([RecuperacaoSenhaId] ASC),
        CONSTRAINT [FK_RecuperacaoSenha_Usuario] FOREIGN KEY ([UsuarioId]) 
            REFERENCES [core].[Usuario] ([UsuarioId]),
        CONSTRAINT [CK_RecuperacaoSenha_Codigo] CHECK (LEN([Codigo]) = 6)
    );

    CREATE NONCLUSTERED INDEX [IX_RecuperacaoSenha_Codigo] 
        ON [core].[RecuperacaoSenha] ([Codigo], [Usado], [DataExpiracao]);

    CREATE NONCLUSTERED INDEX [IX_RecuperacaoSenha_Usuario] 
        ON [core].[RecuperacaoSenha] ([TenantId], [UsuarioId], [DataCriacao] DESC);
    
    CREATE NONCLUSTERED INDEX [IX_RecuperacaoSenha_Usado] 
        ON [core].[RecuperacaoSenha] ([Usado])
        WHERE [Usado] = 0;

    PRINT 'Tabela core.RecuperacaoSenha criada com sucesso!';
END
ELSE
BEGIN
    PRINT 'Tabela core.RecuperacaoSenha já existe.';
END
GO

