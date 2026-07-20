import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { SlotConfigApiService } from '../../core/services/slot-config.service';
import { ErrorHandlerService } from '../../core/services/error-handler.service';
import { SLOT_NAMES, SlotConfigDto } from '../../core/models/slot-config.models';

/**
 * Admin screen to configure the four daily attendance time slots
 * (Requirements 2.1–2.5, 8.5). Edit-in-place: clicking a row loads it into
 * the form above; Save calls PUT, and the "New" button clears the form for
 * a POST.
 */
@Component({
  standalone: false,
  selector: 'app-slot-config',
  templateUrl: './slot-config.component.html',
  styleUrls: ['./slot-config.component.scss']
})
export class SlotConfigComponent implements OnInit {
  slots: SlotConfigDto[] = [];
  loading = false;
  editingSlotId: number | null = null;
  slotNames = SLOT_NAMES;

  form: ReturnType<FormBuilder['group']>;

  constructor(
    private readonly fb: FormBuilder,
    private readonly slotApi: SlotConfigApiService,
    private readonly errorHandler: ErrorHandlerService
  ) {
    this.form = this.fb.group({
      slotName: ['MorningIn', Validators.required],
      startTime: ['08:00', Validators.required],
      endTime: ['09:00', Validators.required],
      gracePeriodMinutes: [0, [Validators.required, Validators.min(0)]],
      isMandatory: [true]
    });
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.slotApi.getAll().subscribe({
      next: slots => {
        this.slots = slots;
        this.loading = false;
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.errorHandler.show(err);
      }
    });
  }

  edit(slot: SlotConfigDto): void {
    this.editingSlotId = slot.slotId;
    this.form.patchValue({
      slotName: slot.slotName,
      startTime: slot.startTime.slice(0, 5),
      endTime: slot.endTime.slice(0, 5),
      gracePeriodMinutes: slot.gracePeriodMinutes,
      isMandatory: slot.isMandatory
    });
  }

  newSlot(): void {
    this.editingSlotId = null;
    this.form.reset({ slotName: 'MorningIn', startTime: '08:00', endTime: '09:00', gracePeriodMinutes: 0, isMandatory: true });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const payload = {
      slotName: raw.slotName!,
      startTime: `${raw.startTime}:00`,
      endTime: `${raw.endTime}:00`,
      gracePeriodMinutes: raw.gracePeriodMinutes!,
      isMandatory: raw.isMandatory!
    };

    const request$ = this.editingSlotId
      ? this.slotApi.update(this.editingSlotId, payload)
      : this.slotApi.create(payload);

    request$.subscribe({
      next: () => {
        this.errorHandler.showSuccess('Slot configuration saved.');
        this.newSlot();
        this.load();
      },
      error: (err: HttpErrorResponse) => this.errorHandler.show(err)
    });
  }

  deactivate(slot: SlotConfigDto): void {
    this.slotApi.deactivate(slot.slotId).subscribe({
      next: () => {
        this.errorHandler.showSuccess(`${slot.slotName} deactivated.`);
        this.load();
      },
      error: (err: HttpErrorResponse) => this.errorHandler.show(err)
    });
  }

  delete(slot: SlotConfigDto): void {
    this.slotApi.delete(slot.slotId).subscribe({
      next: () => {
        this.errorHandler.showSuccess(`${slot.slotName} deleted.`);
        if (this.editingSlotId === slot.slotId) {
          this.newSlot();
        }
        this.load();
      },
      error: (err: HttpErrorResponse) => this.errorHandler.show(err)
    });
  }

  /**
   * Formats a stored 24-hour "HH:mm[:ss]" time as 12-hour with AM/PM
   * (e.g. "13:00:00" -> "1:00 PM") so the table matches the time input widget.
   */
  formatTime(time: string): string {
    const [hourStr, minuteStr] = time.split(':');
    const hours = Number(hourStr);
    const minutes = minuteStr ?? '00';
    const period = hours < 12 ? 'AM' : 'PM';
    const hour12 = hours % 12 === 0 ? 12 : hours % 12;
    return `${hour12}:${minutes} ${period}`;
  }
}
