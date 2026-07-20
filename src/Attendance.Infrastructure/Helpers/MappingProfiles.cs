using Attendance.Application.DTOs;
using Attendance.Infrastructure.Models;
using AutoMapper;

namespace Attendance.Infrastructure.Helpers;

/// <summary>
/// AutoMapper profile that maps Infrastructure entity types to Application-layer DTOs.
/// Lives in Infrastructure because Infrastructure references Application (not vice-versa),
/// ensuring no circular project dependency.
/// </summary>
public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        // ── Staff → StaffDto ─────────────────────────────────────────────────────────
        // Satisfies Requirement 1.2 — all mandatory profile fields surfaced in the DTO.
        // Department name is resolved via the navigation property (Staff.Department.DepartmentName).
        // PhotoUrl is mapped from the optional StaffProfile.PhotoPath.
        CreateMap<Staff, StaffDto>()
            .ForMember(dest => dest.Department,
                opt => opt.MapFrom(src => src.Department.DepartmentName))
            .ForMember(dest => dest.PhotoUrl,
                opt => opt.MapFrom(src => src.Profile != null ? src.Profile.PhotoPath : null))
            .ForMember(dest => dest.Status,
                opt => opt.MapFrom(src => (int)src.Status));

        // ── AttendanceSlotConfig → SlotConfigDto ─────────────────────────────────────
        // Satisfies Requirements 2.1–2.4 — all slot configuration fields map 1-to-1.
        CreateMap<AttendanceSlotConfig, SlotConfigDto>();

        // ── AttendanceLog → AttendanceRecordDto ──────────────────────────────────────
        // Satisfies Requirements 3.4 and 3.12 — the scan response includes staff name,
        // slot name, timestamp, and status label resolved from navigation properties.
        // GreetingMessage is a computed field built by the service layer; it cannot be
        // derived purely from entity data, so it is left as an empty string here and
        // overwritten by the service after mapping.
        CreateMap<AttendanceLog, AttendanceRecordDto>()
            .ForMember(dest => dest.StaffName,
                opt => opt.MapFrom(src => src.Staff.FullName))
            .ForMember(dest => dest.SlotName,
                opt => opt.MapFrom(src => src.Slot.SlotName))
            .ForMember(dest => dest.StatusLabel,
                opt => opt.MapFrom(src => src.StatusFlag == Attendance.Infrastructure.Enums.AttendanceStatus.OnTime
                    ? "On Time"
                    : "Late"))
            .ForMember(dest => dest.GreetingMessage,
                opt => opt.Ignore());

        // ── QrSession → QrCodeResponseDto ────────────────────────────────────────────
        // Satisfies Requirements 3.1 and 3.2.
        // QrImageBase64 is a generated PNG image that is never stored in the database;
        // it is produced by QRCoder at generation time and set by the service layer.
        // Map only the fields available on the entity; service sets QrImageBase64 after.
        CreateMap<QrSession, QrCodeResponseDto>()
            .ForMember(dest => dest.QrImageBase64,
                opt => opt.Ignore());
    }
}
