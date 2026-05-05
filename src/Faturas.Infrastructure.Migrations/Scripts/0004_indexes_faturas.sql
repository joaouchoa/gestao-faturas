-- Índice para busca por nome do cliente (filtro RN-10)
CREATE INDEX IF NOT EXISTS ix_faturas_nome_cliente
    ON faturas (nome_cliente);

-- Índice para busca por data de emissão (filtro RN-10)
CREATE INDEX IF NOT EXISTS ix_faturas_data_emissao
    ON faturas (data_emissao);

-- Índice para busca por status (filtro RN-10)
CREATE INDEX IF NOT EXISTS ix_faturas_status
    ON faturas (status);

-- Índice para joins entre itens e faturas
CREATE INDEX IF NOT EXISTS ix_itens_fatura_fatura_id
    ON itens_fatura (fatura_id);
