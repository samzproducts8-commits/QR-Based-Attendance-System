export interface ScanRequestDto {
  token: string;
}

export interface AttendanceRecordDto {
  staffName: string;
  slotName: string;
  eventTimestamp: string;
  statusLabel: string;
  greetingMessage: string;
}

export interface QrCodeResponseDto {
  tokenValue: string;
  qrImageBase64: string;
  expiresAt: string;
}

export interface AttendanceHistoryEntry {
  attendanceLogId: number;
  eventDate: string;
  slotName: string;
  eventTimestamp: string;
  statusLabel: string;
}

export interface DailySlotEntry {
  slotId: number;
  slotName: string;
  eventTimestamp: string | null;
  statusLabel: string;
  absenceReason?: string | null;
}

export interface DailyAttendanceSheet {
  staffId: number;
  staffName: string;
  date: string;
  entries: DailySlotEntry[];
}

export interface SlotMonthlySummary {
  slotId: number;
  slotName: string;
  onTimeCount: number;
  lateCount: number;
  absentCount: number;
}

export interface StaffMonthlySummary {
  staffId: number;
  staffName: string;
  department: string;
  slotSummaries: SlotMonthlySummary[];
}

export interface MonthlySummary {
  year: number;
  month: number;
  staffSummaries: StaffMonthlySummary[];
}

/** Structured API error shape (RFC 7807 ProblemDetails) returned by the backend. */
export interface ProblemDetails {
  status: number;
  title: string;
  detail: string;
  instance?: string;
}
