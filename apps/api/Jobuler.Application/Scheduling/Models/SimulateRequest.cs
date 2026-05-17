namespace Jobuler.Application.Scheduling.Models;

/// <summary>
/// Request body for the simulation endpoint.
/// Contains a complete SolverInputDto payload to forward to the solver.
/// </summary>
public record SimulateRequest(SolverInputDto Payload);
