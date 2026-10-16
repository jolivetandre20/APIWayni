namespace Wayni.Deudores.Admin.Web.Services;

// DTOs of the microservices' public contracts (the Admin depends on HTTP contracts, not on their internals).

public sealed record JobDto(
    Guid Id, string FileName, string Status, string ObjectKey, DateTime CreatedAt, DateTime? StartedAt, DateTime? FinishedAt,
    int? Periodo, long LineasLeidas, long LineasInvalidas, int Deudores, int Entidades, decimal TotalPrestamos, long? DuracionMs,
    string? Error);

public sealed record InboxFileDto(string Name, long SizeBytes);

public sealed record DeudorDto(
    string NroIdentificacion, int SituacionMaxima, string SituacionDescripcion, decimal SumaTotalPrestamos, int CantidadRegistros, int Periodo);

public sealed record EntidadDto(int CodigoEntidad, string? Nombre, decimal SumaTotalPrestamos, int CantidadRegistros, int? Periodo);

public sealed record SituacionDto(int Codigo, string Descripcion);
