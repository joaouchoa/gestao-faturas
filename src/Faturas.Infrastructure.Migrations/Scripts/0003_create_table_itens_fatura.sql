CREATE TABLE IF NOT EXISTS itens_fatura (
    id                  UUID            NOT NULL DEFAULT gen_random_uuid(),
    fatura_id           UUID            NOT NULL,
    descricao           VARCHAR(500)    NOT NULL,
    quantidade          INTEGER         NOT NULL,
    valor_unitario      NUMERIC(18, 2)  NOT NULL,
    valor_total_item    NUMERIC(18, 2)  NOT NULL,
    justificativa       TEXT            NULL,

    CONSTRAINT pk_itens_fatura      PRIMARY KEY (id),
    CONSTRAINT fk_itens_fatura_fatura
        FOREIGN KEY (fatura_id)
        REFERENCES faturas (id)
        ON DELETE CASCADE
);
