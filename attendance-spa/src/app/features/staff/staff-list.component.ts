import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { StaffApiService } from '../../core/services/staff.service';
import { ErrorHandlerService } from '../../core/services/error-handler.service';
import { StaffDto } from '../../core/models/staff.models';
import { DataTableAction, DataTableColumn } from '../../shared/data-table/data-table.component';

/**
 * Staff list with search/filter and pagination (Requirement 1.2, 5.4).
 */
@Component({
  standalone: false,
  selector: 'app-staff-list',
  templateUrl: './staff-list.component.html',
  styleUrls: ['./staff-list.component.scss']
})
export class StaffListComponent implements OnInit {
  staff: StaffDto[] = [];
  loading = false;
  searchText = '';

  columns: DataTableColumn[] = [
    { key: 'uniqueCode', header: 'Code' },
    { key: 'fullName', header: 'Name' },
    { key: 'department', header: 'Department' },
    { key: 'jobTitle', header: 'Job Title' },
    {
      key: 'status',
      header: 'Status',
      format: r => (r.status === 1 ? 'Active' : 'Inactive'),
      chip: r => (r.status === 1 ? 'ok' : 'muted')
    }
  ];

  actions: DataTableAction[] = [
    { key: 'delete', icon: 'delete', tooltip: 'Delete employee', color: 'warn' }
  ];

  constructor(
    private readonly staffApi: StaffApiService,
    private readonly errorHandler: ErrorHandlerService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.staffApi.getAll({ pageNumber: 1, pageSize: 100, searchText: this.searchText || null }).subscribe({
      next: result => {
        this.staff = result.items;
        this.loading = false;
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.errorHandler.show(err);
      }
    });
  }

  onRowClick(row: StaffDto): void {
    this.router.navigate(['/staff', row.staffId]);
  }

  onAction(event: { action: string; row: StaffDto }): void {
    if (event.action === 'delete') {
      this.deleteStaff(event.row);
    }
  }

  private deleteStaff(row: StaffDto): void {
    const confirmed = window.confirm(
      `Permanently delete ${row.fullName} (${row.uniqueCode})?\n\n` +
        'This also removes their login and all attendance history and cannot be undone.'
    );
    if (!confirmed) {
      return;
    }

    this.staffApi.delete(row.staffId).subscribe({
      next: () => {
        this.errorHandler.showSuccess(`${row.fullName} was deleted.`);
        this.load();
      },
      error: (err: HttpErrorResponse) => this.errorHandler.show(err)
    });
  }

  createNew(): void {
    this.router.navigate(['/staff/new']);
  }
}
