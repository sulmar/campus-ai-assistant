namespace CampusAssistant.Domain;

/// <summary>Informacja, czy automat z kawą jest czynny. <see cref="Unknown"/> jest pierwsze, więc to wartość domyślna.</summary>
public enum Availability
{
    Unknown,
    InService,
    OutOfService,
}
