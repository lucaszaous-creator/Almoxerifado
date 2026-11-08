namespace ALMOXPRO.Domain.Exceptions;

/// <summary>Violação de regra de negócio da camada de domínio.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Lançada quando uma operação exige mais estoque do que o disponível.</summary>
public class InsufficientStockException : DomainException
{
    public InsufficientStockException(string productName, decimal requested, decimal available)
        : base($"Estoque insuficiente para o produto '{productName}'. Solicitado: {requested:N2}, disponível: {available:N2}.")
    {
    }
}
