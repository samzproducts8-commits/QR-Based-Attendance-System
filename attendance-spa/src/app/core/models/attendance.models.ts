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
  excusedAbsentCount: number;
  unexcusedAbsentCount: number;
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

export interface RecentActivity {
  attendanceLogId: number;
  staffId: number;
  staffName: string;
  slotName: string;
  eventTimestamp: string;
  statusLabel: string;
}

export interface LiveDashboardMetrics {
  date: string;
  totalActiveStaff: number;
  totalActiveCheckIns: number;
  lateArrivals: number;
  onLeaveEmployees: number;
  unexcusedAbsences: number;
  recentActivities: RecentActivity[];
}

export interface StaffPayrollSummary {
  staffId: number;
  uniqueCode: string;
  fullName: string;
  department: string;
  totalDaysWorked: number;
  totalHours: number;
  overtimeHours: number;
  latePenalties: number;
  excusedAbsences: number;
  unpaidAbsences: number;
}

export interface MonthlyPayrollSummary {
  year: number;
  month: number;
  totalStaff: number;
  totalDaysWorked: number;
  totalHoursWorked: number;
  totalOvertimeHours: number;
  totalLatePenalties: number;
  totalExcusedAbsences: number;
  totalUnpaidAbsences: number;
  staffSummaries: StaffPayrollSummary[];
}

/** Structured API error shape (RFC 7807 ProblemDetails) returned by the backend. */
export interface ProblemDetails {
  status: number;
  title: string;
  detail: string;
  instance?: string;
}
