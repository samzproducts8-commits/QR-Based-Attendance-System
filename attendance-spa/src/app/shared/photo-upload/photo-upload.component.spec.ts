import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PhotoUploadComponent } from './photo-upload.component';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';

function makeFile(name: string, type: string, content = 'x'): File {
  return new File([content], name, { type });
}

function fireFileChange(component: PhotoUploadComponent, file: File | null): void {
  const dataTransfer = new DataTransfer();
  if (file) dataTransfer.items.add(file);
  const input = document.createElement('input');
  input.type = 'file';
  Object.defineProperty(input, 'files', { value: dataTransfer.files });
  component.onFileChange({ target: input } as unknown as Event);
}

describe('PhotoUploadComponent', () => {
  let fixture: ComponentFixture<PhotoUploadComponent>;
  let component: PhotoUploadComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [PhotoUploadComponent],
      imports: [CommonModule, MatIconModule, MatButtonModule]
    });
    fixture = TestBed.createComponent(PhotoUploadComponent);
    component = fixture.componentInstance;
  });

  it('emits the file and clears the error when a valid PNG is selected', () => {
    let emitted: File | null = null;
    component.fileSelected.subscribe(f => (emitted = f));

    const pngFile = makeFile('photo.png', 'image/png');
    fireFileChange(component, pngFile);

    expect(emitted).not.toBeNull();
    expect(component.errorMessage).toBeNull();
  });

  it('does NOT emit and shows an error when a non-PNG file is selected', () => {
    let emitted: File | null = null;
    component.fileSelected.subscribe(f => (emitted = f));

    const jpegFile = makeFile('photo.jpg', 'image/jpeg');
    fireFileChange(component, jpegFile);

    expect(emitted).toBeNull();
    expect(component.errorMessage).toBe('Only PNG images are accepted.');
  });

  it('does NOT emit when the extension is .png but the MIME type is spoofed', () => {
    let emitted: File | null = null;
    component.fileSelected.subscribe(f => (emitted = f));

    const spoofedFile = makeFile('photo.png', 'image/jpeg');
    fireFileChange(component, spoofedFile);

    expect(emitted).toBeNull();
    expect(component.errorMessage).toBe('Only PNG images are accepted.');
  });
});
