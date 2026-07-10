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
