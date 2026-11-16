-- =====================================================================
-- FEEDBACK: opinião do cliente sobre um produto ou sobre o atendimento
-- Executar após 01_schema.sql. O script pode ser executado mais de uma vez
-- (IF NOT EXISTS): a aplicação o aplica automaticamente em bancos já existentes.
-- =====================================================================

CREATE TABLE IF NOT EXISTS feedback (
    id_feedback  INT GENERATED ALWAYS AS IDENTITY,
    id_usuario   INT           NOT NULL,
    id_pedido    INT,
    id_produto   INT,
    assunto      VARCHAR(20)   NOT NULL,
    nota         SMALLINT      NOT NULL,
    comentario   VARCHAR(1000) NOT NULL,
    data_envio   TIMESTAMPTZ   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    lido         BOOLEAN       NOT NULL DEFAULT FALSE,

    CONSTRAINT pk_feedback           PRIMARY KEY (id_feedback),
    CONSTRAINT fk_feedback_usuario   FOREIGN KEY (id_usuario)
        REFERENCES usuario (id_usuario) ON DELETE CASCADE,
    CONSTRAINT fk_feedback_pedido    FOREIGN KEY (id_pedido)
        REFERENCES pedido (id_pedido) ON DELETE SET NULL,
    CONSTRAINT fk_feedback_produto   FOREIGN KEY (id_produto)
        REFERENCES produto (id_produto) ON DELETE SET NULL,
    CONSTRAINT ck_feedback_assunto   CHECK (assunto IN ('PRODUTO', 'ATENDIMENTO')),
    CONSTRAINT ck_feedback_nota      CHECK (nota BETWEEN 1 AND 5),
    CONSTRAINT ck_feedback_comentario CHECK (LENGTH(TRIM(comentario)) >= 3),
    -- Feedback sobre produto precisa indicar qual produto
    CONSTRAINT ck_feedback_produto   CHECK (assunto <> 'PRODUTO' OR id_produto IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS ix_feedback_usuario ON feedback (id_usuario);
CREATE INDEX IF NOT EXISTS ix_feedback_lido    ON feedback (lido);
