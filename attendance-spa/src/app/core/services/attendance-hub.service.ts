import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface KioskQrUpdate {
  base64Png: string;
  tokenValue: string;
  expiresAt: string;
}

export type HubConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'error';

/**
 * Wraps the SignalR connection to /hubs/attendance so the kiosk component
 * receives live QR code pushes (Requirements 6.1–6.5).
 *
 * The hub connection is deliberately anonymous — no JWT is attached — since
 * the kiosk is a public, unattended display and the QR payload it receives
 * carries no security value (see AttendanceHub / docs/QR-Security.md).
 */
@Injectable({ providedIn: 'root' })
export class AttendanceHubService {
  private connection: signalR.HubConnection | null = null;

  private readonly qrUpdateSubject = new BehaviorSubject<KioskQrUpdate | null>(null);
  readonly qrUpdate$: Observable<KioskQrUpdate | null> = this.qrUpdateSubject.asObservable();

  private readonly statusSubject = new BehaviorSubject<HubConnectionStatus>('disconnected');
  readonly status$: Observable<HubConnectionStatus> = this.statusSubject.asObservable();

  private readonly errorSubject = new BehaviorSubject<string | null>(null);
  readonly error$: Observable<string | null> = this.errorSubject.asObservable();

  /**
   * Connects to the hub. Never throws — connection failures (server
   * unreachable, network drop, etc.) are surfaced via {@link status$}
   * (= 'error') and {@link error$} instead of an unhandled promise
   * rejection, so the UI can show a clear message instead of
   * "Waiting for QR code…" forever.
   */
  async connect(): Promise<void> {
    if (this.connection) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect()
      .build();

    this.connection.on('ReceiveQrCode', (base64Png: string, tokenValue: string, expiresAt: string) => {
      this.qrUpdateSubject.next({ base64Png, tokenValue, expiresAt });
    });

    this.connection.onreconnecting(() => this.statusSubject.next('reconnecting'));
    this.connection.onreconnected(() => {
      this.statusSubject.next('connected');
      this.requestCurrentQr();
    });
    this.connection.onclose(() => this.statusSubject.next('disconnected'));

    this.errorSubject.next(null);
    this.statusSubject.next('connecting');

    try {
      await this.connection.start();
      this.statusSubject.next('connected');
      await this.requestCurrentQr();
    } catch {
      this.connection = null;
      this.statusSubject.next('error');
      this.errorSubject.next('Could not connect to the server. Retrying…');
    }
  }

  async requestCurrentQr(): Promise<void> {
    await this.connection?.invoke('RequestCurrentQr');
  }

  async disconnect(): Promise<void> {
    await this.connection?.stop();
    this.connection = null;
    this.statusSubject.next('disconnected');
  }
}
