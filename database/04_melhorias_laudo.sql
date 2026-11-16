-- =====================================================================
-- Melhorias do Laudo de Qualidade (Situação-problema 3)
-- Executar após 03_feedback.sql. Pode ser executado mais de uma vez:
-- a aplicação o aplica automaticamente em bancos novos e já existentes.
--   Item 2 - telefone em formato válido
--   Item 3 - recuperação de senha (tabela redefinicao_senha)
--   Item 6 - nome de produto único
--   Item 7 - foto do produto enviada pelo administrador (tabela produto_imagem)
-- =====================================================================

-- ---------------------------------------------------------------------
-- Item 6: renomeia produtos com nome repetido que já existam ("Nome (2)",
-- "Nome (3)"...) e cria a regra de nome único, sem diferenciar maiúsculas
-- de minúsculas nem espaços nas pontas.
-- ---------------------------------------------------------------------
DO $$
DECLARE
    r        RECORD;
    n        INT;
    novo     TEXT;
BEGIN
    IF to_regclass('public.uq_produto_nome') IS NOT NULL THEN
        RETURN;
    END IF;
    FOR r IN
        SELECT id_produto, nome
          FROM (SELECT id_produto, nome,
                       ROW_NUMBER() OVER (PARTITION BY LOWER(TRIM(nome)) ORDER BY id_produto) AS ordem
                  FROM produto) t
         WHERE ordem > 1
         ORDER BY id_produto
    LOOP
        n := 2;
        LOOP
            novo := LEFT(TRIM(r.nome), 112) || ' (' || n || ')';
            EXIT WHEN NOT EXISTS (SELECT 1 FROM produto WHERE LOWER(TRIM(nome)) = LOWER(novo));
            n := n + 1;
        END LOOP;
        UPDATE produto SET nome = novo WHERE id_produto = r.id_produto;
        RAISE NOTICE 'Produto % renomeado para "%"', r.id_produto, novo;
    END LOOP;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_produto_nome ON produto (LOWER(TRIM(nome)));

-- ---------------------------------------------------------------------
-- Item 2: telefone no formato "(11) 98888-7777" (celular) ou "(11) 3333-4444".
-- Telefones antigos são convertidos para esse formato quando têm DDD + número
-- válidos; os que não têm conserto (ex.: letras) são apagados, pois o campo
-- é opcional. Depois a regra passa a valer para todos os registros.
-- ---------------------------------------------------------------------
DO $$
DECLARE
    r   RECORD;
    d   TEXT;
    fmt TEXT;
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_usuario_telefone') THEN
        RETURN;
    END IF;
    FOR r IN SELECT id_usuario, telefone FROM usuario WHERE telefone IS NOT NULL LOOP
        d := regexp_replace(r.telefone, '[^0-9]', '', 'g');
        IF length(d) IN (12, 13) AND left(d, 2) = '55' THEN d := substr(d, 3); END IF;   -- +55
        IF length(d) IN (11, 12) AND left(d, 1) = '0' THEN d := substr(d, 2); END IF;  -- 0 + DDD
        fmt := NULL;
        IF d ~ '^[1-9][0-9]9[0-9]{8}$' THEN
            fmt := '(' || substr(d, 1, 2) || ') ' || substr(d, 3, 5) || '-' || substr(d, 8, 4);
        ELSIF d ~ '^[1-9][0-9][2-8][0-9]{7}$' THEN
            fmt := '(' || substr(d, 1, 2) || ') ' || substr(d, 3, 4) || '-' || substr(d, 7, 4);
        END IF;
        IF fmt IS DISTINCT FROM r.telefone THEN
            UPDATE usuario SET telefone = fmt WHERE id_usuario = r.id_usuario;
            RAISE NOTICE 'Telefone do usuário % ajustado: "%" -> "%"', r.id_usuario, r.telefone, COALESCE(fmt, '(vazio)');
        END IF;
    END LOOP;
    ALTER TABLE usuario ADD CONSTRAINT ck_usuario_telefone
        CHECK (telefone IS NULL OR telefone ~ '^\([1-9][0-9]\) (9[0-9]{4}|[2-8][0-9]{3})-[0-9]{4}$');
END $$;

-- ---------------------------------------------------------------------
-- Item 3: pedidos de redefinição de senha. Guarda só o hash (SHA-256) do
-- código; o código em si nunca fica no banco. Uso único e prazo de validade.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS redefinicao_senha (
    id_redefinicao INT GENERATED ALWAYS AS IDENTITY,
    id_usuario     INT          NOT NULL,
    token_hash     CHAR(64)     NOT NULL,
    criado_em      TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expira_em      TIMESTAMPTZ  NOT NULL,
    usado_em       TIMESTAMPTZ,

    CONSTRAINT pk_redefinicao_senha    PRIMARY KEY (id_redefinicao),
    CONSTRAINT fk_redefinicao_usuario  FOREIGN KEY (id_usuario)
        REFERENCES usuario (id_usuario) ON DELETE CASCADE,
    CONSTRAINT uq_redefinicao_token    UNIQUE (token_hash),
    CONSTRAINT ck_redefinicao_prazo    CHECK (expira_em > criado_em)
);

CREATE INDEX IF NOT EXISTS ix_redefinicao_usuario ON redefinicao_senha (id_usuario);

-- ---------------------------------------------------------------------
-- Item 7: foto do produto enviada pelo administrador, guardada no próprio
-- banco (não se perde ao publicar em hospedagens com disco temporário).
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS produto_imagem (
    id_produto   INT          NOT NULL,
    conteudo     BYTEA        NOT NULL,
    tipo_mime    VARCHAR(20)  NOT NULL,
    tamanho      INT          NOT NULL,
    data_envio   TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_produto_imagem       PRIMARY KEY (id_produto),
    CONSTRAINT fk_produto_imagem       FOREIGN KEY (id_produto)
        REFERENCES produto (id_produto) ON DELETE CASCADE,
    CONSTRAINT ck_produto_imagem_tipo  CHECK (tipo_mime IN ('image/jpeg', 'image/png', 'image/webp')),
    CONSTRAINT ck_produto_imagem_tam   CHECK (tamanho > 0 AND tamanho <= 2097152)
);
