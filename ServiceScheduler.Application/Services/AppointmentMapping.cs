using System.Security.Cryptography;
using System.Text;
using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Domain.Entities;

namespace ServiceScheduler.Application.Services;

internal static class AppointmentMapping
{
    public static AppointmentDto ToDto(this Appointment a) => new(
        a.Id, a.CustomerId, a.VehicleId, a.DealershipId, a.ServiceTypeId, a.TechnicianId, a.ServiceBayId,
        a.StartAt, a.EndAt, a.Status.ToString(), a.CreatedAt, a.CancelledAt);

    /// <summary>Fingerprint of a request's meaning. The start is the UTC instant, so the same moment sent with any offset hashes the same.</summary>
    public static string Hash(BookAppointmentRequest r) =>
        Sha256($"book|{r.CustomerId:N}|{r.VehicleId:N}|{r.DealershipId:N}|{r.ServiceTypeId:N}|{r.StartAt.UtcTicks}");

    public static string HashCancel(Guid appointmentId) => Sha256($"cancel|{appointmentId:N}");

    private static string Sha256(string canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}
