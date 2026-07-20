export interface StaffDto {
  staffId: number;
  uniqueCode: string;
  fullName: string;
  department: string;
  jobTitle: string;
  status: number;
  photoUrl: string | null;
}

export interface CreateStaffRequest {
  fullName: string;
  gender: string;
  dateOfBirth: string;
  phoneNumber: string;
  email: string;
  departmentId: number;
  jobTitle: string;
  employmentDate: string;
  address?: string | null;
  emergencyContact?: string | null;
}

export type UpdateStaffRequest = CreateStaffRequest;

export interface StaffFilterRequest {
  department?: string | null;
  status?: number | null;
  searchText?: string | null;
  pageNumber: number;
  pageSize: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface DepartmentDto {
  departmentId: number;
  departmentName: string;
  isActive: boolean;
}
