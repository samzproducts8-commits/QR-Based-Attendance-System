import { Component, EventEmitter, Input, Output } from '@angular/core';

const PNG_MIME = 'image/png';

/**
 * File input restricted to PNG uploads. Performs a client-side extension +
 * MIME-type check before emitting the file (Requirement 8.1) — this is a UX
 * convenience only; the authoritative magic-byte check happens server-side
 * in PhotoValidationHelper (Requirement 1.4).
 */
@Component({
  standalone: false,
  selector: 'app-photo-upload',
  templateUrl: './photo-upload.component.html',
  styleUrls: ['./photo-upload.component.scss']
})
export class PhotoUploadComponent {
  @Input() label = 'Profile Photo (PNG only)';
  @Output() fileSelected = new EventEmitter<File>();

  previewUrl: string | null = null;
  errorMessage: string | null = null;

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file name after an error

    if (!file) {
      return;
    }

    const extensionValid = file.name.toLowerCase().endsWith('.png');
    const mimeValid = file.type === PNG_MIME;

    if (!extensionValid || !mimeValid) {
      this.errorMessage = 'Only PNG images are accepted.';
      this.previewUrl = null;
      return;
    }

    this.errorMessage = null;
    this.previewUrl = URL.createObjectURL(file);
    this.fileSelected.emit(file);
  }
}
