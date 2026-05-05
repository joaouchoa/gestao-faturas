CREATE TABLE IF NOT EXISTS faturas (
    id              UUID            NOT NULL DEFAULT gen_random_uuid(),
    numero          VARCHAR(20)     NOT NULL,
    nome_cliente    VARCHAR(150)    NOT NULL,
    data_emissao    TIMESTAMPTZ     NOT NULL,
    status          INTEGER         NOT NULL DEFAULT 0,   -- 0 = Aberta, 1 = Fechada
    valor_total     NUMERIC(18, 2)  NOT NULL DEFAULT 0,

    CONSTRAINT pk_faturas PRIMARY KEY (id),
    CONSTRAINT uq_faturas_numero UNIQUE (numero)
);
