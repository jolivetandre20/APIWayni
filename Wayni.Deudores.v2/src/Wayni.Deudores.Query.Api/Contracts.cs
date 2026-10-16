using Wayni.Deudores.Core.Domain;
using Wayni.Deudores.Infrastructure.Persistence.Entities;

namespace Wayni.Deudores.Query.Api;

/// <summary>Amounts are in thousands of pesos with one decimal, as published by the BCRA.</summary>
public sealed record DeudorResponse(
    string NroIdentificacion, int SituacionMaxima, string SituacionDescripcion, decimal SumaTotalPrestamos,
    int CantidadRegistros, int Periodo)
{
    public static DeudorResponse From(Deudor d) => new(d.NroIdentificacion, d.SituacionMaxima, Situaciones.Describe(d.SituacionMaxima),
        d.SumaTotalPrestamos, d.CantidadRegistros, d.Periodo);
}

public sealed record EntidadResponse(int CodigoEntidad, string? Nombre, decimal SumaTotalPrestamos, int CantidadRegistros, int? Periodo)
{
    public static EntidadResponse From(Entidad e) => new(e.CodigoEntidad, e.Nombre, e.SumaTotalPrestamos, e.CantidadRegistros, e.Periodo);
}

public sealed record SituacionResponse(int Codigo, string Descripcion);
