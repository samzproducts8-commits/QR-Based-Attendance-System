import { AfterViewInit, Component, NgZone, OnDestroy } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { Html5Qrcode } from 'html5-qrcode';
import { AttendanceApiService } from '../../core/services/attendance.service';
import { ErrorHandlerService } from '../../core/services/error-handler.service';
import { AttendanceRecordDto } from '../../core/models/attendance.models';

type ScanOutcome =
  | { status: 'pending' }
  | { status: 'success'; payload: AttendanceRecordDto }
  | { status: 'error'; payload: string };

/** Live-camera readiness state. */
type CameraState =
  | 'idle'        // not started yet
  | 'starting'    // requesting permission / spinning up
  | 'scanning'    // camera live, looking for a QR
  | 'denied'      // user refused camera permission
  | 'unavailable' // no camera on the device
  | 'insecure'    // page not served over HTTPS/localhost — browser blocks the camera
  | 'error';      // any other camera failure

/**
 * Mobile scan page. By default it opens the phone's rear camera and scans
 * the kiosk QR live (Requirement 3.3 / 8.4); on a successful read it extracts
 * the token and records attendance. A manual code-entry fallback is always
 * available (and is auto-used when the page is opened via a `?token=` deep
 * link from a native camera app). Camera access requires a secure context
 * (HTTPS or localhost) — when that is missing the UI says so clearly and
 * falls back to manual entry.
 */
@Component({
  standalone: false,
  selector: 'app-scan-confirm',
  templateUrl: './scan-confirm.component.html',
  styleUrls: ['./scan-confirm.component.scss']
})
export class ScanConfirmComponent implements AfterViewInit, OnDestroy {
  /** DOM id the html5-qrcode library renders the camera preview into. */
  readonly readerElementId = 'qr-camera-reader';

  token = '';
  submitting = false;
  result: AttendanceRecordDto | null = null;
  errorMessage: string | null = null;

  cameraState: CameraState = 'idle';
  /** When true the manual code-entry field is shown instead of the camera. */
  manualEntry = false;
  /** True only on the deep-link (?token=) path, where no camera is needed. */
  private cameFromDeepLink = false;

  private scanner: Html5Qrcode | null = null;

  constructor(
    private readonly attendanceApi: AttendanceApiService,
    private readonly errorHandler: ErrorHandlerService,
    private readonly zone: NgZone,
    route: ActivatedRoute
  ) {
    const tokenFromUrl = route.snapshot.queryParamMap.get('token');
    if (tokenFromUrl) {
      // Opened by a native camera app scanning the kiosk QR: token already in
      // the URL, so skip the in-app camera and record immediately.
      this.cameFromDeepLink = true;
      this.token = tokenFromUrl;
      this.hydrateOrSubmit(tokenFromUrl);
    }
  }

  ngAfterViewInit(): void {
    // Deep-link path handles itself in the constructor; otherwise start the
    // live camera once the preview element exists. Deferred a tick so the
    // camera-state mutation lands in its own change-detection pass (avoids
    // NG0100 ExpressionChangedAfterItHasBeenChecked in dev mode).
    if (!this.cameFromDeepLink) {
      setTimeout(() => void this.startCamera(), 0);
    }
  }

  ngOnDestroy(): void {
    void this.stopCamera();
  }

  // -------------------------------------------------------------------------
  // Live camera scanning
  // -------------------------------------------------------------------------

  async startCamera(): Promise<void> {
    this.manualEntry = false;
    this.errorMessage = null;

    // Secure-context guard: getUserMedia simply does not exist on an insecure
    // origin (plain http:// that is not localhost). Tell the user precisely
    // why and fall back to manual entry instead of a cryptic failure.
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
      this.cameraState = 'insecure';
      this.manualEntry = true;
      return;
    }

    this.cameraState = 'starting';

    try {
      this.scanner ??= new Html5Qrcode(this.readerElementId, /* verbose */ false);
      await this.scanner.start(
        { facingMode: 'environment' },
        {
          fps: 10,
          // Scan box tracks the video size (80% of the shorter edge) so the
          // whole kiosk QR comfortably fits regardless of phone screen size.
          qrbox: (viewW: number, viewH: number) => {
            const edge = Math.floor(Math.min(viewW, viewH) * 0.8);
            return { width: edge, height: edge };
          }
        },
        decodedText => this.onScanSuccess(decodedText),
        () => { /* per-frame "no QR in view" — ignore */ }
      );
      this.zone.run(() => (this.cameraState = 'scanning'));
    } catch (err: unknown) {
      this.zone.run(() => this.handleCameraError(err));
    }
  }

  private handleCameraError(err: unknown): void {
    const name = (err as { name?: string })?.name ?? '';
    const text = `${name} ${String((err as { message?: string })?.message ?? err)}`.toLowerCase();

    if (text.includes('notallowed') || text.includes('permission') || text.includes('denied')) {
      this.cameraState = 'denied';
    } else if (text.includes('notfound') || text.includes('no camera') || text.includes('devices')) {
      this.cameraState = 'unavailable';
    } else {
      this.cameraState = 'error';
    }
    // Whatever went wrong, let them still record attendance by typing the code.
    this.manualEntry = true;
  }

  private onScanSuccess(decodedText: string): void {
    const token = ScanConfirmComponent.extractToken(decodedText);
    if (!token) {
      // Not one of our QR codes — keep scanning.
      return;
    }

    // Stop the camera first (single shot) then submit inside Angular's zone.
    void this.stopCamera().then(() => {
      this.zone.run(() => {
        this.token = token;
        this.submit();
      });
    });
  }

  private async stopCamera(): Promise<void> {
    if (!this.scanner) return;
    try {
      // isScanning guard avoids "Cannot stop, scanner is not running" throws.
      if (this.scanner.isScanning) {
        await this.scanner.stop();
      }
      this.scanner.clear();
    } catch {
      /* best-effort teardown */
    } finally {
      this.scanner = null;
      if (this.cameraState === 'scanning' || this.cameraState === 'starting') {
        this.cameraState = 'idle';
      }
    }
  }

  /** Show the manual code field (e.g. user taps "Enter code manually"). */
  useManualEntry(): void {
    this.manualEntry = true;
    void this.stopCamera();
  }

  /**
   * Extracts the attendance token from scanned QR text. The kiosk encodes a
   * deep-link URL (…/scan?token=&lt;guid&gt;), but this also accepts a bare
   * GUID or a GUID embedded anywhere in the text, so it is robust to however
   * the code is generated. Returns null when no GUID is present.
   */
  static extractToken(text: string): string | null {
    const guid = /[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/;

    try {
      const url = new URL(text.trim());
      const fromQuery = url.searchParams.get('token');
      if (fromQuery && guid.test(fromQuery)) return fromQuery;
    } catch {
      /* not a URL — fall through to a bare-GUID match */
    }

    const match = text.match(guid);
    return match ? match[0] : null;
  }

  // -------------------------------------------------------------------------
  // Recording (unchanged core + duplicate-safe outcome caching)
  // -------------------------------------------------------------------------

  submit(): void {
    if (!this.token) {
      this.errorMessage = 'Please scan or enter the QR code value.';
      return;
    }

    this.submitting = true;
    this.result = null;
    this.errorMessage = null;

    this.attendanceApi.scan({ token: this.token }).subscribe({
      next: record => {
        this.submitting = false;
        this.result = record;
        this.cacheOutcome({ status: 'success', payload: record });
      },
      error: (err: HttpErrorResponse) => {
        this.submitting = false;
        const message = this.errorHandler.extractMessage(err);
        this.errorMessage = message;
        this.cacheOutcome({ status: 'error', payload: message });
      }
    });
  }

  /** Clear the result and restart the live camera for the next employee. */
  reset(): void {
    this.token = '';
    this.result = null;
    this.errorMessage = null;
    this.manualEntry = false;
    this.cameraState = 'idle';
    if (!this.cameFromDeepLink) {
      // Defer one tick so Angular re-renders the camera preview element
      // before html5-qrcode tries to attach to it.
      setTimeout(() => void this.startCamera(), 0);
    }
  }

  /**
   * Deep-link (?token=) duplicate-safe path: caches the outcome of an
   * auto-submitted token so a page reload replays the real result instead of
   * firing a second (guaranteed-to-fail) scan for the same single-use token.
   */
  private hydrateOrSubmit(token: string): void {
    const key = ScanConfirmComponent.outcomeKey(token);
    const cached = ScanConfirmComponent.readOutcome(key);

    if (cached?.status === 'success') {
      this.result = cached.payload;
      return;
    }
    if (cached?.status === 'error') {
      this.errorMessage = cached.payload;
      return;
    }
    if (cached?.status === 'pending') {
      this.submitting = true;
      this.pollForOutcome(key);
      return;
    }

    sessionStorage.setItem(key, JSON.stringify({ status: 'pending' } satisfies ScanOutcome));
    this.submit();
  }

  private pollForOutcome(key: string, attemptsLeft = 15): void {
    const outcome = ScanConfirmComponent.readOutcome(key);

    if (outcome && outcome.status !== 'pending') {
      this.submitting = false;
      if (outcome.status === 'success') this.result = outcome.payload;
      else this.errorMessage = outcome.payload;
      return;
    }

    if (attemptsLeft <= 0) {
      this.submitting = false;
      this.errorMessage = 'Still processing — check your attendance history if this persists.';
      return;
    }

    setTimeout(() => this.pollForOutcome(key, attemptsLeft - 1), 300);
  }

  private cacheOutcome(outcome: ScanOutcome): void {
    if (!this.token) return;
    sessionStorage.setItem(ScanConfirmComponent.outcomeKey(this.token), JSON.stringify(outcome));
  }

  private static outcomeKey(token: string): string {
    return `scan_outcome_${token}`;
  }

  private static readOutcome(key: string): ScanOutcome | null {
    const raw = sessionStorage.getItem(key);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as ScanOutcome;
    } catch {
      return null;
    }
  }
}
