-- =====================================================================
-- PIT Engenharia de Software II - Loja de Cupcakes Gourmet
-- Script físico do banco de dados (PostgreSQL 14+)
-- Datas em TIMESTAMPTZ (gravadas em UTC, exibidas no fuso do usuário)
-- Aluno: Denis Santana Ribeiro dos Santos - RGM 33146802
-- =====================================================================
-- Decisões de escopo refletidas neste modelo:
--   * Produtos organizados por categoria (Tradicionais, Veganos, Especiais).
--   * Cupom de desconto fora do escopo.
--   * Pagamento simulado: o cliente escolhe a forma de pagamento e paga
--     na entrega ou na retirada (não há integração com gateway).
--   * O valor total e os subtotais são calculados no servidor (RNF04).
-- =====================================================================

DROP TABLE IF EXISTS item_pedido CASCADE;
DROP TABLE IF EXISTS pedido      CASCADE;
DROP TABLE IF EXISTS produto     CASCADE;
DROP TABLE IF EXISTS categoria   CASCADE;
DROP TABLE IF EXISTS usuario     CASCADE;

-- ---------------------------------------------------------------------
-- USUARIO: clientes e administradores da loja
-- ---------------------------------------------------------------------
CREATE TABLE usuario (
    id_usuario     INT GENERATED ALWAYS AS IDENTITY,
    nome           VARCHAR(120) NOT NULL,
    email          VARCHAR(180) NOT NULL,
    telefone       VARCHAR(20),
    senha_hash     VARCHAR(255) NOT NULL,
    perfil         VARCHAR(20)  NOT NULL DEFAULT 'CLIENTE',
    data_cadastro  TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_usuario        PRIMARY KEY (id_usuario),
    CONSTRAINT uq_usuario_email  UNIQUE (email),
    CONSTRAINT ck_usuario_perfil CHECK (perfil IN ('CLIENTE', 'ADMIN')),
    CONSTRAINT ck_usuario_email  CHECK (email = LOWER(email) AND email LIKE '%_@_%.__%'),
    CONSTRAINT ck_usuario_nome   CHECK (LENGTH(TRIM(nome)) >= 2)
);

-- ---------------------------------------------------------------------
-- CATEGORIA: agrupamento dos produtos exibido nas abas do catálogo
-- ---------------------------------------------------------------------
CREATE TABLE categoria (
    id_categoria  INT GENERATED ALWAYS AS IDENTITY,
    nome          VARCHAR(60)  NOT NULL,
    descricao     VARCHAR(255),
    ordem_exibicao INT         NOT NULL DEFAULT 0,
    ativo         BOOLEAN      NOT NULL DEFAULT TRUE,

    CONSTRAINT pk_categoria      PRIMARY KEY (id_categoria),
    CONSTRAINT uq_categoria_nome UNIQUE (nome)
);

-- ---------------------------------------------------------------------
-- PRODUTO: cupcakes oferecidos pela loja
-- ---------------------------------------------------------------------
CREATE TABLE produto (
    id_produto          INT GENERATED ALWAYS AS IDENTITY,
    id_categoria        INT           NOT NULL,
    nome                VARCHAR(120)  NOT NULL,
    descricao           TEXT,
    preco               NUMERIC(10,2) NOT NULL,
    quantidade_estoque  INT           NOT NULL DEFAULT 0,
    imagem_url          VARCHAR(255),
    ativo               BOOLEAN       NOT NULL DEFAULT TRUE,
    data_cadastro       TIMESTAMPTZ   NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_produto           PRIMARY KEY (id_produto),
    CONSTRAINT fk_produto_categoria FOREIGN KEY (id_categoria)
        REFERENCES categoria (id_categoria) ON DELETE RESTRICT,
    CONSTRAINT ck_produto_preco     CHECK (preco >= 0),
    CONSTRAINT ck_produto_estoque   CHECK (quantidade_estoque >= 0)
);

CREATE INDEX ix_produto_categoria ON produto (id_categoria);

-- ---------------------------------------------------------------------
-- PEDIDO: compra registrada por um cliente
-- ---------------------------------------------------------------------
CREATE TABLE pedido (
    id_pedido         INT GENERATED ALWAYS AS IDENTITY,
    id_usuario        INT           NOT NULL,
    data_pedido       TIMESTAMPTZ   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    data_atualizacao  TIMESTAMPTZ   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status            VARCHAR(30)   NOT NULL DEFAULT 'RECEBIDO',
    valor_total       NUMERIC(10,2) NOT NULL,
    tipo_entrega      VARCHAR(20)   NOT NULL,
    endereco_entrega  VARCHAR(255),
    forma_pagamento   VARCHAR(20)   NOT NULL,
    observacao        VARCHAR(255),

    CONSTRAINT pk_pedido          PRIMARY KEY (id_pedido),
    CONSTRAINT fk_pedido_usuario  FOREIGN KEY (id_usuario)
        REFERENCES usuario (id_usuario) ON DELETE RESTRICT,
    CONSTRAINT ck_pedido_status   CHECK (status IN (
        'RECEBIDO', 'EM_PREPARO', 'PRONTO_PARA_RETIRADA',
        'SAIU_PARA_ENTREGA', 'ENTREGUE', 'CANCELADO')),
    CONSTRAINT ck_pedido_total    CHECK (valor_total >= 0),
    CONSTRAINT ck_pedido_tipo     CHECK (tipo_entrega IN ('ENTREGA', 'RETIRADA')),
    CONSTRAINT ck_pedido_pagto    CHECK (forma_pagamento IN ('DINHEIRO', 'PIX', 'CARTAO')),
    -- Pedido para entrega exige endereço preenchido
    CONSTRAINT ck_pedido_endereco CHECK (
        tipo_entrega = 'RETIRADA'
        OR (endereco_entrega IS NOT NULL AND LENGTH(TRIM(endereco_entrega)) >= 5)),
    -- Status coerente com o tipo de entrega
    CONSTRAINT ck_pedido_status_tipo CHECK (
        NOT (tipo_entrega = 'RETIRADA' AND status = 'SAIU_PARA_ENTREGA')
        AND NOT (tipo_entrega = 'ENTREGA' AND status = 'PRONTO_PARA_RETIRADA'))
);

CREATE INDEX ix_pedido_usuario ON pedido (id_usuario);
CREATE INDEX ix_pedido_status  ON pedido (status);

-- ---------------------------------------------------------------------
-- ITEM_PEDIDO: produtos de cada pedido (classe associativa Pedido x Produto)
-- O preço unitário é copiado do produto no momento da compra, preservando
-- o histórico mesmo que o preço mude depois.
-- ---------------------------------------------------------------------
CREATE TABLE item_pedido (
    id_item         INT GENERATED ALWAYS AS IDENTITY,
    id_pedido       INT           NOT NULL,
    id_produto      INT           NOT NULL,
    quantidade      INT           NOT NULL,
    preco_unitario  NUMERIC(10,2) NOT NULL,
    subtotal        NUMERIC(10,2) GENERATED ALWAYS AS (quantidade * preco_unitario) STORED,

    CONSTRAINT pk_item_pedido         PRIMARY KEY (id_item),
    CONSTRAINT fk_item_pedido_pedido  FOREIGN KEY (id_pedido)
        REFERENCES pedido (id_pedido) ON DELETE CASCADE,
    CONSTRAINT fk_item_pedido_produto FOREIGN KEY (id_produto)
        REFERENCES produto (id_produto) ON DELETE RESTRICT,
    CONSTRAINT uq_item_pedido_produto UNIQUE (id_pedido, id_produto),
    CONSTRAINT ck_item_quantidade     CHECK (quantidade > 0),
    CONSTRAINT ck_item_preco          CHECK (preco_unitario >= 0)
);

CREATE INDEX ix_item_pedido_produto ON item_pedido (id_produto);
