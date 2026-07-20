import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { AttendanceHubService, HubConnectionStatus, KioskQrUpdate } from '../../core/services/attendance-hub.service';

/**
 * Full-screen kiosk display: continuously shows the live, auto-refreshing
 * QR code pushed over SignalR (Requirements 6.1–6.5, 8.3). No navigation
 * chrome — this route is excluded from the app shell's sidenav.
 */
@Component({
  standalone: false,
  selector: 'app-kiosk',
  templateUrl: './kiosk.component.html',
  styleUrls: ['./kiosk.component.scss']
})
export class KioskComponent implements OnInit, OnDestroy {
  qrImageSrc: string | null = null;
  status: HubConnectionStatus = 'disconnected';
  errorMessage: string | null = null;

  private subscriptions = new Subscription();

  constructor(private readonly hub: AttendanceHubService) {}

  ngOnInit(): void {
    this.subscriptions.add(
      this.hub.qrUpdate$.subscribe((update: KioskQrUpdate | null) => {
        if (update) {
          this.qrImageSrc = `data:image/png;base64,${update.base64Png}`;
        }
      })
    );

    this.subscriptions.add(this.hub.status$.subscribe(status => (this.status = status)));
    this.subscriptions.add(this.hub.error$.subscribe(message => (this.errorMessage = message)));

    this.connect();
  }

  /** Public so the template's Retry button can call it directly. */
  connect(): void {
    this.hub.connect();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.hub.disconnect();
  }
}
