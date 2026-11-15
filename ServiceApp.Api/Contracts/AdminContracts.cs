using System.ComponentModel.DataAnnotations;

namespace ServiceApp.Api.Contracts;

/// <summary>POST /api/admin/requests/{id}/assign</summary>
public record AssignDto([Range(1, int.MaxValue)] int TechnicianProfileId);
