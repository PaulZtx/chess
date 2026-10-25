namespace ChessInfrastructure.Entities;

/// <summary>
/// Сущность с постоянным идентификатором
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Идентификатор сущности, который нельзя изменить после создания
    /// </summary>
    public Guid Id { get; private set; } = Guid.NewGuid();
}
