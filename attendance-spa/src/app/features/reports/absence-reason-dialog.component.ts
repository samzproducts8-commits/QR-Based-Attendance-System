import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

export interface AbsenceReasonDialogData {
  staffName: string;
  slotName: string;
  date: string;
  currentReason?: string | null;
}

@Component({
  standalone: false,
  selector: 'app-absence-reason-dialog',
  templateUrl: './absence-reason-dialog.component.html',
  styleUrls: ['./absence-reason-dialog.component.scss']
})
export class AbsenceReasonDialogComponent {
  reason: string;

  constructor(
    public dialogRef: MatDialogRef<AbsenceReasonDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AbsenceReasonDialogData
  ) {
    this.reason = data.currentReason || '';
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  onSave(): void {
    if (this.reason.trim()) {
      this.dialogRef.close(this.reason.trim());
    }
  }
}
