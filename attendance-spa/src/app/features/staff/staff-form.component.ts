import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { DepartmentApiService, StaffApiService } from '../../core/services/staff.service';
import { ErrorHandlerService } from '../../core/services/error-handler.service';
import { DepartmentDto } from '../../core/models/staff.models';

/**
 * Reactive form for creating (and, when an id route param is present,
 * updating) a staff member. Photo upload is mandatory for creation only
 * (Requirements 1.1–1.8, 8.1, 8.2).
 */
@Component({
  standalone: false,
  selector: 'app-staff-form',
  templateUrl: './staff-form.component.html',
  styleUrls: ['./staff-form.component.scss']
})
export class StaffFormComponent implements OnInit {
  departments: DepartmentDto[] = [];
  selectedPhoto: File | null = null;
  submitting = false;
  isEditMode = false;
  private staffId: number | null = null;

  form: ReturnType<FormBuilder['group']>;

  constructor(
    private readonly fb: FormBuilder,
    private readonly staffApi: StaffApiService,
    private readonly departmentApi: DepartmentApiService,
    private readonly errorHandler: ErrorHandlerService,
    private readonly router: Router,
    private readonly route: ActivatedRoute
  ) {
    this.form = this.fb.group({
      fullName: ['', Validators.required],
      gender: ['', Validators.required],
      dateOfBirth: ['', Validators.required],
      phoneNumber: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      departmentId: [null as number | null, Validators.required],
      jobTitle: ['', Validators.required],
      employmentDate: ['', Validators.required],
      address: [''],
      emergencyContact: ['']
    });
  }

  ngOnInit(): void {
    this.departmentApi.getAll().subscribe({
      next: departments => (this.departments = departments),
      error: (err: HttpErrorResponse) => this.errorHandler.show(err)
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.staffId = Number(idParam);
      this.staffApi.getById(this.staffId).subscribe({
        next: staff => {
          // Only fields present on StaffDto are patched; the rest the admin re-enters if changing.
          this.form.patchValue({ fullName: staff.fullName, jobTitle: staff.jobTitle });
        },
        error: (err: HttpErrorResponse) => this.errorHandler.show(err)
      });
    }
  }

  onPhotoSelected(file: File): void {
    this.selectedPhoto = file;
  }

  submit(): void {
    if (this.form.invalid || (!this.isEditMode && !this.selectedPhoto)) {
      this.form.markAllAsTouched();
      if (!this.selectedPhoto && !this.isEditMode) {
        this.errorHandler.showMessage('A PNG profile photo is required.');
      }
      return;
    }

    const raw = this.form.getRawValue();
    const payload = {
      fullName: raw.fullName!,
      gender: raw.gender!,
      dateOfBirth: this.toIsoDate(raw.dateOfBirth),
      phoneNumber: raw.phoneNumber!,
      email: raw.email!,
      departmentId: raw.departmentId!,
      jobTitle: raw.jobTitle!,
      employmentDate: this.toIsoDate(raw.employmentDate),
      address: raw.address || null,
      emergencyContact: raw.emergencyContact || null
    };

    this.submitting = true;

    const request$ = this.isEditMode
      ? this.staffApi.update(this.staffId!, payload)
      : this.staffApi.create(payload, this.selectedPhoto!);

    request$.subscribe({
      next: staff => {
        this.submitting = false;
        this.errorHandler.showSuccess(`Staff ${staff.uniqueCode} saved.`);
        this.router.navigate(['/staff', staff.staffId]);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting = false;
        this.errorHandler.show(err);
      }
    });
  }

  /**
   * The Material datepicker (with MatNativeDateModule) stores its value as a
   * native JS Date object, not a string. The backend expects an ISO
   * "yyyy-MM-dd" DateOnly string, so this converts using local date parts
   * (never UTC) to avoid an off-by-one-day shift for users east of UTC.
   */
  private toIsoDate(value: unknown): string {
    if (value instanceof Date) {
      const year = value.getFullYear();
      const month = String(value.getMonth() + 1).padStart(2, '0');
      const day = String(value.getDate()).padStart(2, '0');
      return `${year}-${month}-${day}`;
    }
    return String(value ?? '');
  }
}
