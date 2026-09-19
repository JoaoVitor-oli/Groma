using Groma.Domain.Exceptions;

namespace Groma.Domain.Catalog;

/// <summary>
/// Uma tabela catalogada. Nasce de uma fonte e acumula versões de schema ao longo
/// do tempo. As versões vivem em <see cref="DatasetVersion"/>; aqui fica só o
/// ponteiro para a mais recente, para não carregar o histórico inteiro a cada leitura.
/// </summary>
public sealed class Dataset
{
    internal Dataset(Guid sourceId, string name, DateTimeOffset createdAt)
    {
        if (sourceId == Guid.Empty)
            throw new DomainException("Dataset precisa pertencer a uma fonte.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do dataset não pode ser vazio.");

        Id = Guid.CreateVersion7();
        SourceId = sourceId;
        Name = name.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid SourceId { get; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Zero enquanto nenhuma carga tiver acontecido.</summary>
    public int CurrentVersionNumber { get; private set; }

    public DateTimeOffset? LastIngestedAt { get; private set; }

    public bool NeverIngested => CurrentVersionNumber == 0;

    /// <summary>
    /// Registra que uma carga terminou. O número vem da versão que a comparação
    /// de schema determinou — a mesma de antes, se nada mudou.
    /// </summary>
    public void RecordIngestion(int versionNumber, DateTimeOffset at)
    {
        if (versionNumber < 1)
            throw new DomainException("Número da versão começa em 1.");

        if (versionNumber < CurrentVersionNumber)
            throw new DomainException(
                $"Dataset '{Name}' já está na versão {CurrentVersionNumber}; não pode voltar para {versionNumber}.");

        CurrentVersionNumber = versionNumber;
        LastIngestedAt = at;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do dataset não pode ser vazio.");

        Name = name.Trim();
    }
}
