import { AfterViewInit, Component, EventEmitter, Input, OnChanges, Output, ViewChild } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';

export type DataTableChipTone = 'ok' | 'warn' | 'bad' | 'info' | 'muted';

export interface DataTableColumn {
  key: string;
  header: string;
  /** Optional formatter for cell display; defaults to raw property access. */
  format?: (row: any) => string;
  /** When set, the cell renders as a status chip with the returned tone. */
  chip?: (row: any) => DataTableChipTone | null;
  /** Right-align the cell content (e.g. numeric columns). */
  align?: 'start' | 'end';
}

export interface DataTableAction {
  /** Identifier emitted via (actionClick) when the button is pressed. */
  key: string;
  /** Material icon name shown on the button. */
  icon: string;
  /** Native tooltip / accessible label. */
  tooltip?: string;
  /** Material button color, e.g. 'warn' for destructive actions. */
  color?: string;
}

/**
 * Generic table with client-side pagination and sort, driven by a column
 * definition array so feature components stay declarative.
 */
@Component({
  standalone: false,
  selector: 'app-data-table',
  templateUrl: './data-table.component.html',
  styleUrls: ['./data-table.component.scss']
})
export class DataTableComponent implements OnChanges, AfterViewInit {
  @Input() columns: DataTableColumn[] = [];
  @Input() rows: any[] = [];
  @Input() actions: DataTableAction[] = [];
  @Input() pageSizeOptions = [10, 20, 50];
  @Input() pageSize = 20;
  @Output() rowClick = new EventEmitter<any>();
  @Output() actionClick = new EventEmitter<{ action: string; row: any }>();

  /** Synthetic column key that holds the row action buttons. */
  readonly actionsColumnKey = '__actions';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  dataSource = new MatTableDataSource<any>([]);

  get displayedColumns(): string[] {
    const keys = this.columns.map(c => c.key);
    return this.actions.length > 0 ? [...keys, this.actionsColumnKey] : keys;
  }

  onActionClick(event: Event, action: string, row: any): void {
    // Keep the row-level (click) handler from also firing.
    event.stopPropagation();
    this.actionClick.emit({ action, row });
  }

  ngOnChanges(): void {
    this.dataSource.data = this.rows;
  }

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
  }

  chipTone(row: any, column: DataTableColumn): DataTableChipTone | null {
    return column.chip ? column.chip(row) : null;
  }

  cellValue(row: any, column: DataTableColumn): string {
    if (column.format) {
      return column.format(row);
    }
    const value = row[column.key];
    return value === null || value === undefined ? '' : String(value);
  }
}
