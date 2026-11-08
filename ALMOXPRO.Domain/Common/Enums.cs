namespace ALMOXPRO.Domain.Common;

public enum UserStatus
{
    Ativo = 1,
    Inativo = 2,
    Bloqueado = 3
}

public enum EntityStatus
{
    Ativo = 1,
    Inativo = 2
}

/// <summary>Tipo de entrada de materiais.</summary>
public enum EntryType
{
    NotaFiscal = 1,
    Compra = 2,
    Doacao = 3,
    Transferencia = 4,
    Devolucao = 5,
    Ajuste = 6
}

/// <summary>Tipo de saída de materiais.</summary>
public enum ExitType
{
    Consumo = 1,
    OrdemServico = 2,
    Transferencia = 3,
    Devolucao = 4,
    Descarte = 5,
    Ajuste = 6
}

/// <summary>Tipo de movimentação no kardex de estoque.</summary>
public enum StockMovementType
{
    Entrada = 1,
    Saida = 2,
    TransferenciaSaida = 3,
    TransferenciaEntrada = 4,
    AjusteInventario = 5
}

/// <summary>Situação de uma requisição de materiais.</summary>
public enum RequisitionStatus
{
    Pendente = 1,
    Atendida = 2,
    Cancelada = 3,
    AtendidaParcial = 4
}

/// <summary>Situação da NF-e recebida em relação à manifestação do destinatário.</summary>
public enum FiscalDocumentStatus
{
    /// <summary>Resumo/nota recebida da SEFAZ, sem manifestação.</summary>
    Recebida = 1,
    /// <summary>Ciência da Operação registrada (libera o download do XML completo).</summary>
    Ciencia = 2,
    Confirmada = 3,
    Desconhecida = 4,
    /// <summary>Recusada: Operação não Realizada (exige justificativa).</summary>
    OperacaoNaoRealizada = 5
}

/// <summary>Situação da NF-e emitida pela própria empresa.</summary>
public enum IssuedNfeStatus
{
    /// <summary>Autorizada pela SEFAZ (uso autorizado).</summary>
    Autorizada = 1,
    /// <summary>Cancelada por evento de cancelamento homologado.</summary>
    Cancelada = 2
}

public enum InventoryType
{
    Geral = 1,
    Rotativo = 2
}

public enum InventoryStatus
{
    Aberto = 1,
    EmContagem = 2,
    Concluido = 3,
    Cancelado = 4
}
